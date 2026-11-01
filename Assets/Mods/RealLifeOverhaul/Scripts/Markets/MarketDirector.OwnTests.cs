namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the market director: the test buttons for the player's own company.</summary>
    public sealed partial class MarketDirector
    {
        /// <summary>
        /// Test button: the player's company on the market as the test tools' company, with nothing asked, paid or raised; a
        /// save with under four quarters of books gets a test company's.
        /// </summary>
        public void TestListing()
        {
            FinishMoment();
            const string Test = "My company on the market";
            if (!TestReady(Test, true))
            {
                return;
            }
            ListingFile f = Listing;
            if (MyCompany != null || f.OnTheWay || _following)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_lst_test_already");
                return;
            }
            int day = Save.Day;
            Named(f);
            if (string.IsNullOrEmpty(f.Name))
            {
                f.Name = MarketText.T("reallife_lst_test_name");
                f.Ticker = ListingRules.TickerOf(f.Name);
            }
            if (ListingBook.Last(f, day, Year, ListingCatalogue.QuartersLeast, out long _, out long _, out int _) < ListingCatalogue.QuartersLeast)
            {
                f.Books.Clear();
                int now = MarketCatalogue.QuarterOf(day, Year);
                for (int d = MarketCatalogue.QuarterStart(now - 8, Year); d < day; d++)
                {
                    ListingBook.AddDay(f, d, (long)(50000 * Level), (long)(8000 * Level), Year);
                }
                f.TestBooks = true;
            }
            f.Test = true;
            ListingFacts facts = Facts();
            double worth = ListingBook.Worth(f, _engine.State.Me, day, Year, _engine.State.Env, facts.Debt);
            f.OldShares = ListingBook.FounderShares(worth, Level);
            f.NewShares = ListingBook.NewShares(f.OldShares, ListingCatalogue.FloatDefault);
            ListingBook.Prospectus(f, ListingCatalogue.FloatDefault, ListingBook.FairCents(f, _engine.State.Me, day, Year, _engine.State.Env, facts.Debt), 1);
            f.AuditDay = day;
            f.Findings = 0;
            f.Stage = ListingStage.Building;
            f.BookFrom = day;
            f.BookEnd = MarketTrading.NextTradingDay(day);
            Logger?.Info(Test + " test: " + f.Name + " (" + f.Ticker + ") is priced at the open of day " + f.BookEnd + " (range " + f.LowCents + " to " + f.HighCents + " cents, " + f.NewShares
                         + " shares offered beside the founder's " + f.OldShares + (f.TestBooks ? ", a test company's books" : string.Empty) + ").");
            Queue(UI.Notification.NotificationType.Info, "reallife_lst_test_listing_started", "day", f.BookEnd.ToString());
            OpenListing();
        }

        /// <summary>Test button: the player's company's results for the quarter so far, out the next market morning.</summary>
        public void TestOwnResults()
        {
            FinishMoment();
            const string Test = "My company's results";
            Stock own = TestOwn(Test);
            if (own == null)
            {
                return;
            }
            if (own.PendingQuarter < 0)
            {
                own.PendingQuarter = own.Quarter;
                own.PendingRev = own.QRev;
                own.PendingNi = own.QNi;
                own.Own.PendingExpect = own.Own.Expect;
                own.QRev = own.QNi = 0.0;
            }
            own.ResultsDay = MarketTrading.NextTradingDay(Save.Day);
            Logger?.Info(Test + " test: " + own.Name + "'s results for the quarter so far are out on the morning of day " + own.ResultsDay + ".");
            OpenListing();
        }

        /// <summary>Test button: the player's company's yearly meeting at today's close, or the next market day's.</summary>
        public void TestOwnMeeting()
        {
            FinishMoment();
            const string Test = "My company's yearly meeting";
            Stock own = TestOwn(Test);
            if (own == null)
            {
                return;
            }
            own.Own.AgmDay = MarketCatalogue.TradingDay(_engine.State.Day) && _engine.State.Phase < MarketCatalogue.PhaseClose
                ? _engine.State.Day : MarketTrading.NextTradingDay(_engine.State.Day);
            Logger?.Info(Test + " test: " + own.Name + "'s shareholders meet at the close of day " + own.Own.AgmDay + ".");
            OpenListing();
        }

        /// <summary>Test button: an investor presses the player's company's board for the demand its figures give, else a payout.</summary>
        public void TestOwnActivist()
        {
            FinishMoment();
            const string Test = "An investor presses my company's board";
            Stock own = TestOwn(Test);
            if (own == null)
            {
                return;
            }
            ActivistDemand demand = ListingVotes.Reason(own, _engine.State.IndexLast, _engine.State.Day + 1000, Year, out double _);
            _engine.PressBoard(own.Id, demand == ActivistDemand.None ? ActivistDemand.Payout : demand);
            Logger?.Info(Test + " test: an investor holds " + own.Own.ActBp + " bp of " + own.Name + " and wants " + own.Own.ActDemand + " at a meeting on day " + own.Own.ActDay + ".");
            OpenListing();
        }

        public void TestOwnBid()
        {
            FinishMoment();
            const string Test = "A bid for my company";
            Stock own = TestOwn(Test);
            if (own == null)
            {
                return;
            }
            if (!_engine.BidFor(own.Id))
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_test_no_company");
                return;
            }
            Logger?.Info(Test + " test: " + own.Name + " bid for at " + own.BidCents + " cents a share, to complete on day " + own.BidEndDay + "; the founder holds " + own.Own.StakeBp(own)
                         + " bp, the threshold is " + ListingCatalogue.TakeoverThresholdBp + " bp" + (own.Own.Test ? " (a test's company: nothing is handed over)" : string.Empty) + ".");
            OpenListing();
        }

        /// <summary>Test button: the test tools' company off the market with nothing paid; only the test's.</summary>
        public void TestOwnOff()
        {
            FinishMoment();
            const string Test = "Take my test company off the market";
            Stock own = TestOwn(Test);
            if (own == null)
            {
                return;
            }
            if (!own.Own.Test)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_lst_test_not_test");
                return;
            }
            _engine.Delist(own.Id, GoneReason.Delisted, own.Last);
            Listing.Stage = ListingStage.None;
            TestBooksGone(Listing);
            Logger?.Info(Test + " test: " + own.Name + " has left the market; nothing was paid.");
            Moved();
        }

        /// <summary>
        /// Test button: the player's company's sector from the last quarter's books now, as a load of a save from before its
        /// business was followed does: listed, the market's review at once; on its way, the advisers' reading. Writes
        /// "Listing check (sector): from X to Y" with PASS when it is where the books put it and its record keeps it.
        /// </summary>
        public void TestSector()
        {
            FinishMoment();
            const string Test = "Listing check (sector)";
            if (!TestReady(Test, true))
            {
                return;
            }
            if (GoingPublic.InSession || CompanyAside)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_lst_test_sector_session");   // in a session the sector moves only at a review, on every machine alike
                return;
            }
            ListingFile f = Listing;
            Stock own = MyCompany;
            if (own == null && f.Stage > ListingStage.Audited)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_lst_test_sector_book");
                return;
            }
            int day = Save.Day;
            if (!Takings(day, out int days))
            {
                TestRefusals.TellKey(Logger, Test, "reallife_lst_test_sector_none");
                return;
            }
            var sorted = new System.Text.StringBuilder(128);
            for (int t = 0; t < _takings.Length; t++)
            {
                if (_takings[t] > 0)
                {
                    sorted.Append(sorted.Length > 0 ? ", " : string.Empty).Append(MarketInputs.Trades[t]).Append(' ').Append(_takings[t]).Append(" (").Append(ListingSector.SectorOf(t)).Append(')');
                }
            }
            MarketSector was, now;
            bool pass;
            if (own != null)
            {
                was = own.Sector;
                MarketSector want = (MarketSector)ListingSector.FromBooks(_takings, (int)was);
                _engine.SectorFromBooks(own, _takings, days, true);
                now = own.Sector;
                Stock back = MarketStore.ReadStockLine(MarketStore.StockLine(_engine.State, own), Year);
                pass = now == want && back != null && back.Sector == now && back.Own != null && back.Own.Mixed && back.Def.Trade == own.Def.Trade;
            }
            else
            {
                was = (MarketSector)MarketValuation.Clamp(f.Sector, 0, (int)MarketSector.Count - 1);
                Classify(f);
                now = (MarketSector)f.Sector;
                pass = (int)now == ListingSector.Leading(_takings, out int _);
            }
            Logger?.Info(Test + ": from " + was + " to " + now + (own != null ? " (" + own.Name + " on the market" : " (the company on its way") + "; the last " + days + " days' takings by trade: "
                         + sorted + "; next review on day " + ListingSector.NextReview(_engine.State.Day, Year) + "): " + (pass ? "PASS" : "FAIL"));
            Queue(UI.Notification.NotificationType.Info, "reallife_lst_test_sector_done", "from", MarketText.Sector(was), "to", MarketText.Sector(now));
            Moved();
            OpenListing();
        }

        private Stock TestOwn(string test)
        {
            if (!TestReady(test, true))
            {
                return null;
            }
            Stock own = MyCompany;
            if (own == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_lst_test_no_company");
            }
            return own;
        }

        // A test company's books are not the player's: once it is gone they are read again from the game's statements.
        private void TestBooksGone(ListingFile f)
        {
            if (f.TestBooks)
            {
                f.Books.Clear();
                f.BooksDay = 0;
                f.TestBooks = false;
                f.Test = false;
            }
        }
    }
}
