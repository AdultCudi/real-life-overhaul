using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The land market's four tests (Options > Tests, Construction; docs/features/construction.md, "Land market"): an empty site of the
    /// player's sold back to the city; an offer to a rival developer accepted; an offer under its ask refused, and raised; a rival's offer
    /// for the player's land accepted. Each works from anywhere at any time on the free space nearest the player, through the real code,
    /// brings forward the answer a day would bring, writes one "Construction check (land market: ...)" line ending PASS or FAIL, and puts
    /// back what it made: the money it moved, the land and the developer's site (nothing of the real market is left changed). Each is a
    /// public static method, so a test runner can call it without a press of its own.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string LandCheck = "Construction check (land market: ";   // each line goes on with its test's name and ")"
        private const string TestLandKey = "reallife_transaction_bld_test_land";

        private static ConstructionDirector LandReady(string test)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, LandCheck + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(LandCheck + test + "): the construction game has not read this save yet: FAIL.");
                return null;
            }
            if (!d.Running || !DistrictLives())
            {
                TestRefusals.Tell(Logger, LandCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(LandCheck + test + "): the construction game is off, or Uptown is not a district at this load: FAIL.");
                return null;
            }
            if (GoingPublic.InSession)
            {
                Failed(LandCheck, test, "reallife_bld_test_session_city", "a Going Public session is on: no land is sold in one");
                return null;
            }
            return d;
        }

        // A test's line: what was measured, then PASS or FAIL, and a notice saying so.
        private bool LandLine(string test, string labelKey, string line, bool pass)
        {
            Logger?.Warn(LandCheck + test + "): " + line + ": " + (pass ? "PASS" : "FAIL") + ".");
            if (pass)
            {
                Changed();
                Notifications.Show(NotificationType.Success, "reallife_bld_test_passed", new Dictionary<string, string>(1) { { "test", ConstructionText.T(labelKey) } }, 8f, null,
                    Guard.Wrap(() => Open(TabLand, 0)), true, false);
            }
            else
            {
                TestRefusals.TellKey(Logger, LandCheck + test + ")", "reallife_bld_test_failed_log", line);
            }
            return pass;
        }

        // The nearest free space of the grid to the player with a price, so many spaces side by side from its first; null with the reason in why.
        private string LandFreeSpace(out string why)
        {
            why = null;
            if (_state.Lots.Count >= BuildCatalogue.LotsMost)
            {
                why = "the player holds the most lots there may be";
                return null;
            }
            Runs(_testRuns);
            var near = ConstructionGame.Player;
            string first = null;
            float best = float.MaxValue;
            for (int i = 0; i < _testRuns.Count; i++)
            {
                float far = Far(_testRuns[i].First, near);
                if (far < best && SitePrice(_testRuns[i].First.Id, 1) > 0L)
                {
                    best = far;
                    first = _testRuns[i].First.Id;
                }
            }
            _testRuns.Clear();
            if (first == null)
            {
                why = "no free space with a price at this load";
            }
            return first;
        }

        // A site of the player's for the test: the free space bought at its price through the game's transaction, held a month with nothing on it.
        private bool LandTestSite(string first, out long price, out string why)
        {
            why = null;
            price = SitePrice(first, 1);
            if (ConstructionGame.Cash < price + 1f)
            {
                why = "the player has less than the " + price + " the test's land costs (it is paid back at the end)";
                return false;
            }
            if (!ConstructionGame.Take(price, TestLandKey, LotWords(first), false))
            {
                why = "the land could not be paid for";
                return false;
            }
            int day = ConstructionGame.Day;
            SiteSpaces(first, 1, _strip);
            _state.Lots.Add(new LotFile { Id = first, Bought = day - 60, Paid = price, Zones = LandRules.ZonesOf(_strip[0].Class), TaxedTo = day, EmptySince = day - 30, Spaces = 1 });
            GrowsChanged();
            return true;
        }

        // A developer's site on a free space for the test: its plans just begun, what it paid the city the space's price; taken away again at the end.
        private GrowFile LandTestRival(string first)
        {
            int day = ConstructionGame.Day;
            SiteSpaces(first, 1, _strip);
            BuildUse use = BuildUse.Shops;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (LandRules.Zoned(LandRules.ZonesOf(_strip[0].Class), (BuildUse)u))
                {
                    use = (BuildUse)u;
                    break;
                }
            }
            var g = new GrowFile
            {
                Id = _state.NewId(), Stage = GrowStage.Plans, Owner = GrowOwner.Builder, Lot = first, Spaces = 1, Use = use, Rival = DevelopmentRules.Rival(first, use), Day = day, Design = NoDesign,
                Price = SitePrice(first, 1), Until = day + 4
            };
            _state.Grows.Add(g);
            GrowsChanged();
            return g;
        }

        private void LandTestPutBack(GrowFile g, LandOffer o, string lot)
        {
            if (o != null)
            {
                _state.Offers.Remove(o);
            }
            if (g != null)
            {
                _state.Grows.Remove(g);
            }
            LotFile held = lot != null ? _state.Lot(lot) : null;
            if (held != null)
            {
                _state.Lots.Remove(held);
            }
            GrowsChanged();
        }

        // ------------------------------------------------------------------ sell an empty site

        /// <summary>
        /// Test: a free space bought at its price and sold back at once: the city pays the fair price, the land is gone from the player's, the space is for sale again, and a
        /// site with a project on it is refused. The price paid is the price got back, so the test costs nothing.
        /// </summary>
        public static bool TestLandSellNow()
        {
            const string test = "an empty site of yours sold back to the city";
            ConstructionDirector d = LandReady(test);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.LandSellNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool LandSellNow(string test)
        {
            string first = LandFreeSpace(out string why);
            if (first == null)
            {
                return Failed(LandCheck, test, "reallife_bld_test_nothing", why);
            }
            BeforeTest();
            float cash0 = ConstructionGame.Cash;
            if (!LandTestSite(first, out long paid, out why))
            {
                return Failed(LandCheck, test, "reallife_bld_answer_no_money", why);
            }
            float held = ConstructionGame.Cash;
            long expect = SellPrice(first), back0 = _state.CityLandBack;
            string answer = Sell(first);
            bool sold = answer == "reallife_land_answer_sold" && _state.Lot(first) == null;
            bool money = Math.Abs(ConstructionGame.Cash - held - expect) < 1f && expect == LandMarketRules.CityPrice(paid, paid) && _state.CityLandBack == back0 + expect;
            Space(first, out LotView space);
            bool again = FreeSpace(space);
            // a site with a plan on it is not bought back
            bool refused;
            if (LandTestSite(first, out long paid2, out _))
            {
                var plan = new ProjectFile { Id = _state.NewId(), Kind = ProjectKind.Plot, Stage = ProjectStage.Design, Lot = first };
                _state.Projects.Add(plan);
                refused = SellBlock(first) == "reallife_land_answer_sell_busy";
                _state.Projects.Remove(plan);
                LandTestPutBack(null, null, first);
                ConstructionGame.Give(paid2, TestLandKey, LotWords(first));   // the land's price back, as it was never kept
            }
            else
            {
                refused = false;
            }
            string line = first + " (paid " + paid + "): " + answer + ", the city paid " + expect + " (cash " + (ConstructionGame.Cash - cash0).ToString("0", CultureInfo.InvariantCulture)
                          + " over the test), the space " + (again ? "is for sale again" : "is NOT for sale again") + ", a planned site is " + (refused ? "refused" : "NOT refused");
            return LandLine(test, "reallife_land_test_sell", line, sold && money && again && refused);
        }

        // ------------------------------------------------------------------ an offer to a rival accepted

        /// <summary>
        /// Test: a developer's site on the nearest free space, an offer at the top of the range made through the real code and answered at once by
        /// the developer's rules: accepted, the land the player's at that price and the developer's plans dropped. The land and the money are put back.
        /// </summary>
        public static bool TestLandOfferAcceptedNow()
        {
            const string test = "an offer to a rival developer accepted";
            ConstructionDirector d = LandReady(test);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.LandOfferAcceptedNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool LandOfferAcceptedNow(string test)
        {
            string first = LandFreeSpace(out string why);
            if (first == null)
            {
                return Failed(LandCheck, test, "reallife_bld_test_nothing", why);
            }
            BeforeTest();
            int day = ConstructionGame.Day;
            GrowFile g = LandTestRival(first);
            LandAsk ask = AskOf(g, day);
            if (ConstructionGame.Cash < ask.High + 1f)
            {
                LandTestPutBack(g, null, null);
                return Failed(LandCheck, test, "reallife_bld_answer_no_money", "the player has less than the " + ask.High + " the test offers (it is paid back at the end)");
            }
            float cash0 = ConstructionGame.Cash;
            string made = Offer(g.Id, ask.High);
            LandOffer o = _state.WaitingOn(first);
            bool waiting = made == "reallife_land_answer_offered" && o != null && o.Due >= day;
            string again = o != null ? OfferBlock(g, ask.High) : null;   // one waiting at a time
            if (o != null)
            {
                o.Due = day;
                AnswerOffer(o, day);
            }
            LotFile mine = _state.Lot(first);
            bool done = o != null && o.State == LandOfferState.Accepted && mine != null && mine.Paid == ask.High
                && g.Stage == GrowStage.Dropped && Math.Abs(cash0 - ConstructionGame.Cash - ask.High) < 1f;
            string line = first + ": ask " + ask.Ask + " (shown " + ask.Low + " to " + ask.High + "), offered " + ask.High + " (" + made + ", a second while it waits: " + (again ?? "allowed") + "), "
                          + (o != null ? o.State.ToString() : "no offer") + "; the land "
                          + (mine != null ? "is the player's for " + mine.Paid : "is NOT the player's") + ", the developer's plans " + g.Stage;
            LandTestPutBack(g, o, first);
            ConstructionGame.Give(ask.High, TestLandKey, LotWords(first));   // the price paid, back
            return LandLine(test, "reallife_land_test_accepted", line, waiting && again == "reallife_land_answer_waiting" && done);
        }

        // ------------------------------------------------------------------ an offer under the ask refused

        /// <summary>
        /// Test: the same site, an offer a thousand under its ask: refused, nothing paid, the developer's plans go on; then the refused offer is raised at once,
        /// while the same offer again is turned away for a few days.
        /// </summary>
        public static bool TestLandOfferRefusedNow()
        {
            const string test = "an offer under the ask refused and raised";
            ConstructionDirector d = LandReady(test);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.LandOfferRefusedNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool LandOfferRefusedNow(string test)
        {
            string first = LandFreeSpace(out string why);
            if (first == null)
            {
                return Failed(LandCheck, test, "reallife_bld_test_nothing", why);
            }
            BeforeTest();
            int day = ConstructionGame.Day;
            GrowFile g = LandTestRival(first);
            LandAsk ask = AskOf(g, day);
            long under = Math.Max(BuildCatalogue.LandRound, ask.Ask - BuildCatalogue.LandRound);
            if (ConstructionGame.Cash < ask.High + 1f)
            {
                LandTestPutBack(g, null, null);
                return Failed(LandCheck, test, "reallife_bld_answer_no_money", "the player has less than the " + ask.High + " the test may offer");
            }
            float cash0 = ConstructionGame.Cash;
            string made = Offer(g.Id, under);
            LandOffer o = _state.WaitingOn(first);
            if (o != null)
            {
                o.Due = day;
                AnswerOffer(o, day);
            }
            bool refused = made == "reallife_land_answer_offered" && o != null && o.State == LandOfferState.Refused && _state.Lot(first) == null && g.Stage == GrowStage.Plans
                           && Math.Abs(cash0 - ConstructionGame.Cash) < 1f;
            string raise = OfferBlock(g, ask.High);                // higher: at once
            string same = OfferBlock(g, under);                    // the same: not for a few days
            bool limits = raise == null && same == "reallife_land_answer_rest";
            string line = first + ": ask " + ask.Ask + ", offered " + under + " (" + made + "), " + (o != null ? o.State.ToString() : "no offer") + ", nothing paid, its plans " + g.Stage
                          + "; a higher offer is " + (raise ?? "allowed") + ", the same again is " + (same ?? "allowed");
            LandTestPutBack(g, o, null);
            return LandLine(test, "reallife_land_test_refused", line, refused && limits);
        }

        // ------------------------------------------------------------------ a rival's offer for the player's land accepted

        /// <summary>
        /// Test: a free space bought and left a month with nothing on it, a developer's offer for it made by the real code (a use its district asks
        /// for, or the first it is zoned for), accepted: the land is the developer's, with its plans for it begun, and the player has the offer
        /// price. The land and the money are put back.
        /// </summary>
        public static bool TestLandRivalOfferNow()
        {
            const string test = "a rival's offer for your land accepted";
            ConstructionDirector d = LandReady(test);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.LandRivalOfferNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool LandRivalOfferNow(string test)
        {
            string first = LandFreeSpace(out string why);
            if (first == null)
            {
                return Failed(LandCheck, test, "reallife_bld_test_nothing", why);
            }
            BeforeTest();
            int day = ConstructionGame.Day;
            float cash0 = ConstructionGame.Cash;
            if (!LandTestSite(first, out long paid, out why))
            {
                return Failed(LandCheck, test, "reallife_bld_answer_no_money", why);
            }
            LandOffer o = LandOfferOnPlayers(day, first, false);
            if (o == null)
            {
                LandTestPutBack(null, null, first);
                ConstructionGame.Give(paid, TestLandKey, LotWords(first));
                return Failed(LandCheck, test, "reallife_bld_test_failed_log",
                    "no developer's offer could be made (the city's designs are being read, or none fills the ground): press again in a moment");
            }
            bool waiting = o.Waiting && o.Side == LandOfferSide.Theirs && o.Price >= paid && o.Due == LandMarketRules.Due(day) && AcceptBlock(o) == null;
            float before = ConstructionGame.Cash;
            string answer = Accept(o.Id);
            GrowFile g = null;
            for (int i = 0; i < _state.Grows.Count; i++)
            {
                g = _state.Grows[i].Lot == first && _state.Grows[i].Stage == GrowStage.Plans && _state.Grows[i].Owner == GrowOwner.Builder ? _state.Grows[i] : g;
            }
            bool done = answer == "reallife_land_answer_accepted_in" && o.State == LandOfferState.Accepted && _state.Lot(first) == null && g != null && g.Use == o.Use && g.Price == o.Price
                        && Math.Abs(ConstructionGame.Cash - before - o.Price) < 1f;
            string line = first + " (paid " + paid + "): " + OfferDeveloper(o) + " offered " + o.Price + " (" + o.Reason + ", " + o.Use + ", " + o.Design + "), " + answer + ", the land " + (g != null
                ? "is the developer's, its plans " + g.Stage : "is NOT the developer's");
            LandTestPutBack(g, o, first);
            float after = ConstructionGame.Cash;
            if (cash0 - after > 0.5f)
            {
                ConstructionGame.Give((long)Math.Round(cash0 - after), TestLandKey, LotWords(first));
            }
            else if (after - cash0 > 0.5f)
            {
                ConstructionGame.Force((long)Math.Round(after - cash0), TestLandKey, LotWords(first), false);   // the premium the developer paid, taken back
            }
            return LandLine(test, "reallife_land_test_rival_offer", line, waiting && done);
        }
    }
}
