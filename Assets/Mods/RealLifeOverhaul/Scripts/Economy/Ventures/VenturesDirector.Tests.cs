using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// The test tools of the options page, and what the save tools ask of Ventures. A test's offers, deals, promises and
    /// holdings are marked as tests, and "Test: put ventures back" takes them out again with the market, the
    /// founder's name and the investors' days as they were before the first test of the session.
    /// </summary>
    public sealed partial class VenturesDirector
    {
        // What the tests may change that is not marked as a test's: kept from before the first test, put back by the last button.
        private sealed class TestBackup
        {
            public int Reputation, Pitches;
            public readonly List<int> Closed = new List<int>(VenturesCatalogue.InvestorsMost);
            public readonly List<int> Pitched = new List<int>(VenturesCatalogue.InvestorsMost);
        }

        private TestBackup _testBackup;

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!_running || _broken || _file == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (_testBackup == null)
            {
                _testBackup = new TestBackup { Reputation = _file.Reputation, Pitches = _file.Pitches };
                for (int i = 0; i < _file.Investors.Count; i++)
                {
                    _testBackup.Closed.Add(_file.Investors[i].ClosedUntil);
                    _testBackup.Pitched.Add(_file.Investors[i].PitchedDay);
                }
            }
            return true;
        }

        // A test tool runs here: a fault in one switches Ventures off for the session like any other, and never reaches the options page.
        private void Guarded(Action test)
        {
            try
            {
                test();
            }
            catch (Exception e)
            {
                Fault("a test tool", e);
            }
        }

        // The business of the player's with the best recent profit; null if they run none.
        private BuildingRegistration BestBusiness(out BusinessFigures figures)
        {
            figures = default(BusinessFigures);
            Books.Businesses(_businesses);
            BuildingRegistration best = null;
            for (int i = 0; i < _businesses.Count; i++)
            {
                BusinessFigures b = VenturesGame.Figures(_businesses[i]);
                if (best == null || b.AvgProfit7 > figures.AvgProfit7)
                {
                    best = _businesses[i];
                    figures = b;
                }
            }
            _businesses.Clear();
            return best;
        }

        private Investor First(InvestorKind kind, Temperament? temper = null)
        {
            for (int i = 0; i < _file.Investors.Count; i++)
            {
                if (_file.Investors[i].Kind == kind && (temper == null || _file.Investors[i].Temper == temper.Value))
                {
                    return _file.Investors[i];
                }
            }
            return null;
        }

        /// <summary>"Test: a tycoon's offer for my best business": the terms the rule gives, or the tycoon's smallest sum if the figures carry none. Marked as a test's.</summary>
        public void TestOffer()
        {
            Guarded(TestOfferNow);
        }

        private void TestOfferNow()
        {
            if (!TestReady("Tycoon's offer"))
            {
                return;
            }
            BusinessFigures b;
            BuildingRegistration r = BestBusiness(out b);
            Investor tycoon = First(InvestorKind.Tycoon);
            if (r == null || tycoon == null)
            {
                Tell(NotificationType.Warning, "reallife_ven_test_no_business", null);
                return;
            }
            string place = Books.Key(r.Address);
            for (int i = _file.Offers.Count - 1; i >= 0; i--)
            {
                if (_file.Offers[i].Place == place || _file.Offers.Count >= VenturesCatalogue.OffersMost)
                {
                    _file.Offers.RemoveAt(i);   // (room for the test's offer)
                }
            }
            int day = Save.Day, amount, share;
            bool carried = OfferRules.Terms(_file, tycoon, place, b.AvgProfit7, VenturesCatalogue.OfferValueDays, OfferRules.InboundShare(tycoon), day, out amount, out share);
            if (!carried)
            {
                amount = tycoon.ChequeLeast;
                share = OfferRules.InboundShare(tycoon);
            }
            Offer offer = OfferRules.Add(_file, tycoon, place, VenturesGame.Name(r), amount, share, day, Level, OfferSource.Inbound);
            offer.Test = true;
            Logger?.Warn("Ventures check (test offer): " + tycoon.Name + " for " + offer.Business + ": " + b.ProfitableDays + " profitable days of 14, " + b.AvgProfit7.ToString("0")
                         + " a day lately; the rule " + (OfferRules.Qualifies(b) ? "would" : "would not") + " have brought an offer and the figures " + (carried ? "carry" : "do not carry")
                         + " the tycoon's smallest sum: " + amount + " for " + share + " bp, growth " + offer.TargetPct + "% in " + offer.TargetDays + " days, answer by day " + offer.LapseDay + ".");
            Badge();
            Tell(NotificationType.Info, "reallife_ven_note_offer", _openInvestors, "name", tycoon.Name, "business", offer.Business, "amount", FinanceText.Money(amount),
                "share", VenturesText.Share(share), "day", offer.LapseDay.ToString());
        }

        /// <summary>"Test: a meeting with an angel / a tycoon / a venture firm": about the best business, or with none about a business not yet open. Whatever it leads to is marked as a test's.</summary>
        public void TestPitch(int kind)
        {
            Guarded(() => TestPitchNow(kind));
        }

        private void TestPitchNow(int kind)
        {
            if (!TestReady("Meeting"))
            {
                return;
            }
            Investor investor = First((InvestorKind)kind);
            if (investor == null)
            {
                return;
            }
            if (_meeting != null && _meeting.Active)
            {
                TestRefusals.Tell(Logger, "Meeting", TestRefusal.ScreenBusy);
                return;
            }
            investor.PitchedDay = 0;
            investor.ClosedUntil = 0;
            BusinessFigures b;
            BuildingRegistration r = BestBusiness(out b);
            string type = null;
            if (r == null && investor.Kind == InvestorKind.Tycoon)
            {
                Tell(NotificationType.Warning, "reallife_ven_test_no_business", null);   // (a tycoon backs no business that is not open)
                return;
            }
            if (r == null)
            {
                var types = new List<string>();
                VenturesGame.StartableTypes(types);
                type = types.Count > 0 ? types[0] : "ba:businesstype_giftshop";
            }
            StartPitch(investor.Id, r != null ? Books.Key(r.Address) : null, type, VenturesCatalogue.BudgetSteps[3], true);
        }

        /// <summary>"Test: list the investors and companies in the log".</summary>
        public void TestList()
        {
            Guarded(TestListNow);
        }

        private void TestListNow()
        {
            if (!TestReady("List"))
            {
                return;
            }
            Logger?.Warn("Ventures: day " + Save.Day + ", seed " + _file.Seed + ", name with investors " + _file.Reputation + ", level " + Level + ", market day " + _file.MarketSteps
                         + ". " + CastLine());   // asked for by a button: written whether or not diagnostics are on
            for (int i = 0; i < _file.Deals.Count; i++)
            {
                Deal d = _file.Deals[i];
                Logger?.Warn("Ventures:   deal " + d.Id + " with " + NameOf(d.InvestorId) + " on " + d.Business + ": " + d.Amount + " for " + d.ShareBp + " bp, paid out " + d.PaidOut
                             + ", " + d.Stage + ", target " + d.TargetProfit + " by day " + d.TargetDay + ", misses " + d.Misses + ", mood " + d.Mood + (d.Test ? " (test)" : string.Empty));
            }
            for (int i = 0; i < _file.Offers.Count; i++)
            {
                Offer o = _file.Offers[i];
                Logger?.Warn("Ventures:   offer " + o.Id + " from " + NameOf(o.InvestorId) + " for " + o.Business + ": " + o.Amount + " for " + o.ShareBp + " bp, answer by day " + o.LapseDay
                             + (o.Test ? " (test)" : string.Empty));
            }
            for (int i = 0; i < _file.Holdings.Count; i++)
            {
                Holding h = _file.Holdings[i];
                Company c = _file.Company(h.CompanyId);
                Logger?.Warn("Ventures:   " + h.Shares + " shares of " + (c != null ? c.Name : "?") + ", cost " + h.Cost + ", dividends " + h.Dividends + (h.Test ? " (test)" : string.Empty));
            }
            Tell(NotificationType.Info, "reallife_ven_test_listed", null, "investors", _file.Investors.Count.ToString(), "companies", _file.Companies.Count.ToString());
        }

        /// <summary>"Test: step the market 30 days": thirty market days at once, with today's news on the first. No dividend is paid. The market as it stood is kept, and is what a save keeps, until it is put back.</summary>
        public void TestMarket()
        {
            Guarded(TestMarketNow);
        }

        private void TestMarketNow()
        {
            if (!TestReady("Market"))
            {
                return;
            }
            if (_file.Backup == null)
            {
                _file.Backup = Market.Backup(_file);
            }
            int days = 0;
            float rate = _file.RateSeen;
            int levers = _file.LeversSeen, elections = _file.ElectionSeen;
            GatherNews();
            _file.RateSeen = rate;   // (what the real day will compare with is left as it was)
            _file.LeversSeen = levers;
            _file.ElectionSeen = elections;
            int phase = VenturesBridges.Climate.Phase, day = Save.Day;
            for (int n = 0; n < 30; n++)
            {
                _log.Length = 0;
                Market.Step(_file, day, phase, n == 0 ? _news : null, _log);
                Logger?.Warn("Ventures check (test market day " + _file.MarketSteps + "): " + _log + ".");
                days++;
            }
            Tell(NotificationType.Info, "reallife_ven_test_stepped", () => Open(VenturesPhone.Market), "days", days.ToString());
        }

        /// <summary>"Test: pay a dividend": every holding is paid its dividend now; with none held, ten test shares of the first company are given first.</summary>
        public void TestDividend()
        {
            Guarded(TestDividendNow);
        }

        private void TestDividendNow()
        {
            if (!TestReady("Dividend"))
            {
                return;
            }
            if (_file.Holdings.Count == 0 && _file.Companies.Count > 0)
            {
                _file.Holdings.Add(new Holding { CompanyId = _file.Companies[0].Id, Shares = 10, Test = true });
            }
            PayDividends(Save.Day);
            Logger?.Warn("Ventures check (test dividend): paid on " + _file.Holdings.Count + " holding(s); in all so far " + _file.DividendsTotal + ".");
        }

        /// <summary>"Test: an investor calls their money": a deal there is, or a test deal on the best business, becomes a sum called back at this level's pace.</summary>
        public void TestCall()
        {
            Guarded(TestCallNow);
        }

        private void TestCallNow()
        {
            if (!TestReady("Call"))
            {
                return;
            }
            Deal deal = null;
            for (int i = 0; i < _file.Deals.Count; i++)
            {
                deal = _file.Deals[i].Stage != DealStage.Called && (deal == null || _file.Deals[i].Test) ? _file.Deals[i] : deal;
            }
            if (deal == null)
            {
                BusinessFigures b;
                BuildingRegistration r = BestBusiness(out b);
                Investor demanding = First(InvestorKind.Tycoon, Temperament.Demanding) ?? First(InvestorKind.Firm);
                if (r == null || demanding == null)
                {
                    Tell(NotificationType.Warning, "reallife_ven_test_no_business", null);
                    return;
                }
                // A test deal: no money is paid in for it, and nothing but the call is shown.
                deal = new Deal
                {
                    Id = _file.NextId++, InvestorId = demanding.Id, Place = Books.Key(r.Address), Business = VenturesGame.Name(r), Amount = demanding.ChequeLeast,
                    ShareBp = OfferRules.InboundShare(demanding), StartDay = Save.Day, Mood = VenturesCatalogue.MoodStart, Misses = 3, Test = true
                };
                _file.Deals.Add(deal);
            }
            DealRules.StartCall(deal, DealRules.CallSum(deal), Level);
            _file.Unseen++;
            Badge();
            Logger?.Warn("Ventures check (test call): deal " + deal.Id + " with " + NameOf(deal.InvestorId) + " on " + deal.Business + (deal.Test ? " (a test deal)" : string.Empty) + ": put in "
                         + deal.Amount + ", paid out " + deal.PaidOut + ", called " + deal.CallOwed + " at " + deal.CallPerDay + " a day over " + VenturesCatalogue.CallDays[Level] + " days.");
            Tell(NotificationType.Error, "reallife_ven_note_called", _openInvestors, "name", NameOf(deal.InvestorId), "business", deal.Business, "amount", FinanceText.Money(deal.CallOwed),
                "per_day", FinanceText.Money(deal.CallPerDay));
        }

        /// <summary>"Test: put ventures back": the tests' offers, deals, promises and holdings go, and the market, the founder's name and the investors' days are as before the first test.</summary>
        public void TestBack()
        {
            Guarded(TestBackNow);
        }

        private void TestBackNow()
        {
            if (!TestReady("Put back"))
            {
                return;
            }
            if (_meeting != null && _meeting.Active)
            {
                _meeting.End();
            }
            int removed = _file.Offers.RemoveAll(o => o.Test) + _file.Deals.RemoveAll(d => d.Test) + _file.Pledges.RemoveAll(p => p.Test) + _file.Holdings.RemoveAll(h => h.Test);
            bool market = _file.Backup != null;
            if (market)
            {
                Market.Restore(_file, _file.Backup);
                _file.Backup = null;
            }
            _file.Reputation = _testBackup.Reputation;
            _file.Pitches = _testBackup.Pitches;
            for (int i = 0; i < _file.Investors.Count && i < _testBackup.Closed.Count; i++)
            {
                _file.Investors[i].ClosedUntil = _testBackup.Closed[i];
                _file.Investors[i].PitchedDay = _testBackup.Pitched[i];
            }
            _testBackup = null;
            Badge();
            Logger?.Warn("Ventures check (tests put back): " + removed + " test offer(s), deal(s), promise(s) and holding(s) removed; the market " + (market ? "is back at market day " + _file.MarketSteps : "was not moved")
                         + "; name with investors " + _file.Reputation + ". Money a test paid (a dividend, an accepted test offer) stays.");
            Tell(NotificationType.Info, "reallife_ven_test_back_done", _openInvestors, "count", removed.ToString());
        }

        // ---------------------------------------------------------------- for the save tools

        /// <summary>
        /// For the save tools ("prepare this save for removing the mod", "reset the mod's data"): shares do not exist
        /// in the plain game, so every holding is sold at today's price with no commission. Deals end with nothing
        /// owed either way, and a sum called back is forgiven; each is listed in the log. Then Ventures stops; the
        /// timer starts it again if an option is on.
        /// </summary>
        public void StopAll()
        {
            try
            {
                if (_running && _file != null && Save != null)
                {
                    _log.Length = 0;
                    int sold = 0;
                    for (int i = _file.Holdings.Count - 1; i >= 0; i--)
                    {
                        Holding h = _file.Holdings[i];
                        Company c = _file.Company(h.CompanyId);
                        int value = c != null && !h.Test ? Market.Value(c, h.Shares) : 0;
                        if (value > 0)
                        {
                            _wallet.Give(value, VenturesMoney.SharesSold, c.Name);
                            sold += value;
                            _log.Append(' ').Append(h.Shares).Append(" of ").Append(c.Name).Append(" for ").Append(value).Append(';');
                        }
                    }
                    _file.Holdings.Clear();
                    for (int i = 0; i < _file.Deals.Count; i++)
                    {
                        Deal d = _file.Deals[i];
                        _log.Append(" the deal with ").Append(NameOf(d.InvestorId)).Append(" on ").Append(d.Business).Append(" ended (put in ").Append(d.Amount).Append(", paid out ")
                            .Append(d.PaidOut).Append(d.Stage == DealStage.Called ? ", " + d.CallOwed + " called and forgiven" : string.Empty).Append(");");
                    }
                    _file.Deals.Clear();
                    _file.Offers.Clear();
                    _file.Pledges.Clear();
                    Logger?.Warn("Ventures: leaving this save. Shares sold for " + sold + " in all, with no commission." + _log);
                }
                StandDown(true);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it.</summary>
        public void Forget()
        {
            _running = false;
            _file = null;
            _testBackup = null;
            if (_meeting != null)
            {
                _meeting.End();
            }
        }
    }
}
