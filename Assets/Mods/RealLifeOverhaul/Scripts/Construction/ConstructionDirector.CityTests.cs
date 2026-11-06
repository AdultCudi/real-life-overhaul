using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The tests of the city around the company (Options > Tests, and Wiring.CityTests for a runner): each works from
    /// anywhere at any time, brings forward what it needs (a lot, a licence, a tender, a bill, a year's end), writes one
    /// "Construction check (...)" line with PASS or FAIL, and returns whether it passed. What they change of the construction
    /// game goes back with "put everything back as before the tests", and with it the court's cases and claims they filed.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private int _lastAyes = -1, _lastNoes = -1;
        private bool _lastCarried;

        private bool CityFail(string test, string why)
        {
            Logger?.Info("Construction check (" + test + "): " + why + ": FAIL.");
            return false;
        }

        private bool CityReady(string test)
        {
            if (!ModState.Loaded || SaveGameManager.Current == null || !_loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return CityFail(test, "not started, the save is not loaded");
            }
            if (!Running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return CityFail(test, "not started, the construction game is off");
            }
            return true;
        }

        // A licence for the test's company: founded and licensed now if it is not (the put back takes it back).
        private void TestLicensed(int day)
        {
            FirmFile f = _state.Firm;
            if (f.Founded <= 0)
            {
                f.Founded = day;
                if (Office(out PlaceView office))
                {
                    f.Office = office.Place;
                }
            }
            if (f.Licence != LicenceState.Granted)
            {
                f.Licence = LicenceState.Granted;
                f.LicenceDay = day;
                f.LicenceUntil = PlanningRules.LicenceUntil(day, ConstructionGame.DaysPerYear);
            }
        }

        /// <summary>Test: an interest in the building code for the council's test: the company founded and licensed if there is none (the put back takes it back).</summary>
        public bool TestInterest()
        {
            if (!Running)
            {
                return false;
            }
            if (!Interest)
            {
                BeforeTest();
                TestLicensed(ConstructionGame.Day);
            }
            return Interest;
        }

        // ------------------------------------------------------------------ the council's zoning

        /// <summary>
        /// Test: a lot of the player's asks the city's council for a use it is not zoned for, and the council hears it now
        /// (the player's own lot, or the district's nearest open lot given for the test).
        /// </summary>
        public bool TestCouncilZoning()
        {
            const string test = "council zoning";
            if (!CityReady(test))
            {
                return false;
            }
            try
            {
                int day = ConstructionGame.Day;
                LotFile l = null;
                for (int i = 0; i < _state.Lots.Count && l == null; i++)
                {
                    LotFile own = _state.Lots[i];
                    l = own.ZoneAsk == 0 && ZonesNow(own) != (1 << (int)BuildUse.Count) - 1 && !IsTestLot(own.Id) ? own : null;
                }
                if (l == null)
                {
                    if (GoingPublic.InSession || !NearestOpenLot(ConstructionGame.Player, out LotView open, out _))
                    {
                        TestRefusals.TellKey(Logger, test, "reallife_bld_test_no_district");
                        return CityFail(test, "no lot of the player's to ask for, and no open lot of the district to give");
                    }
                    BeforeTest();
                    l = new LotFile { Id = open.Id, Bought = day, Zones = LandRules.ZonesOf(open.Class), TaxedTo = day, EmptySince = day };
                    _state.Lots.Add(l);
                }
                BeforeTest();
                int zones = ZonesNow(l);
                BuildUse use = BuildUse.Homes;
                for (int u = (int)BuildUse.Count - 1; u >= 0; u--)
                {
                    use = !LandRules.Zoned(zones, (BuildUse)u) ? (BuildUse)u : use;
                }
                l.ZoneAsk = BuildCatalogue.Zone(use);
                l.ZoneDay = day;
                _lastAyes = _lastNoes = -1;
                HearZoning(l, true);
                bool decided = l.ZoneAsk == 0;
                bool zoned = LandRules.Zoned(ZonesNow(l), use);
                bool council = _lastAyes >= 0;
                bool counted = !council || _lastAyes + _lastNoes == 5;
                bool pass = decided && counted && zoned == _lastCarried;
                Logger?.Info("Construction check (council zoning): lot " + l.Id + " asked for " + use + ", heard " + (council ? "by the city's council, " + _lastAyes + " to " + _lastNoes
                    : "by the planners (no council)") + ", " + (_lastCarried ? "carried" : "refused") + "; zoned for it now " + zoned + ": " + (pass ? "PASS" : "FAIL") + ".");
                TestDone("the council heard a zoning application for " + use + " now", TabCity, 0);
                return pass;
            }
            catch (Exception e)
            {
                Fault("a test (" + test + ")", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ rigged tenders

        /// <summary>
        /// Test: two business tenders with a ring on each and the player's company asked to cover (licensed for the test if it
        /// is not): it covers on the first, which is awarded and found out by the city's inquiry (a bid rigging case), and
        /// reports the second, whose ring's bids are struck out at the award.
        /// </summary>
        public bool TestRing()
        {
            const string test = "ring";
            if (!CityReady(test))
            {
                return false;
            }
            if (GoingPublic.IsGuest)
            {
                TestRefusals.TellKey(Logger, test, "reallife_bld_test_host");
                return CityFail(test, "not started, a guest of a Going Public session has no tenders");
            }
            try
            {
                BeforeTest();
                int day = ConstructionGame.Day;
                TestLicensed(day);
                TenderFile joined = TestTenderNow(day, 0), reported = TestTenderNow(day, 1);
                RingFile a = TestRingOn(joined), b = TestRingOn(reported);
                string join = RingJoin(joined), report = RingReport(reported);
                Award(joined, day);
                Award(reported, day);
                if (a.Found == 0)
                {
                    RingFound(a, joined, day);   // the inquiry finds it out now, as it may on any day of its weeks
                }
                bool struck = reported.Winner < 0 || !RingRules.Member(b.Members, reported.Winner);
                bool pass = join == "reallife_bld_answer_ring_joined" && report == "reallife_bld_answer_ring_reported" && a.Awarded == day && a.Found == day && b.Found == day && struck
                            && (a.Case > 0 || !CourtOpen());
                Logger?.Info("Construction check (ring): tender " + joined.Id + " covered for (" + (a.LeaderWon ? "the leader won, the cut " + a.Cut : "the leader lost")
                             + "), found out with case " + a.Case + "; tender " + reported.Id + " reported, won by " + reported.Winner
                             + (struck ? ", the ring struck out" : ", a member of the ring") + ": " + (pass ? "PASS" : "FAIL") + ".");
                TestDone("two rigged tenders: one covered for and found out, one reported", TabTenders, joined.Id);
                return pass;
            }
            catch (Exception e)
            {
                Fault("a test (" + test + ")", e);
                return false;
            }
        }

        // A business's tender made now for a test, its bids closing at once. It names no building of the city's, so a rival's
        // win sets out no works anywhere and the put back leaves the street as it was.
        private TenderFile TestTenderNow(int day, int salt)
        {
            int now = day * 24 + ConstructionGame.Hour;
            var t = new TenderFile { Id = _state.NewId(), Source = TenderSource.Client, CivicKind = 4, Use = BuildUse.Shops, Area = 1200, Client = salt, Opened = now, Closes = now };
            int storeys = Math.Max(1, t.Area / 300);
            t.Hours = PlansRules.Hours(t.Use, t.Area, storeys, ProjectKind.Rebuild);
            Exchange(_exchange);
            t.Value = TenderRules.BuildingValue(PlansRules.Estimate(ProjectKind.Rebuild, t.Use, t.Area, storeys, false, 1, ConstructionGame.Prices, _exchange).Total);
            AddTender(t);
            return t;
        }

        private RingFile TestRingOn(TenderFile t)
        {
            RingFile g = _state.Ring(t.Id);
            if (g == null)
            {
                g = new RingFile { Tender = t.Id };
                g.Members = RingRules.Ring(_state.Seed, t.Id, out g.Leader);
                _state.Rings.Add(g);
            }
            g.Asked = true;
            g.Answer = RingFile.NoAnswer;
            return g;
        }

        // ------------------------------------------------------------------ unpaid bills

        /// <summary>
        /// Test: a client's bill a week overdue that it will not pay is chased and taken to court, the court hears it now and
        /// the bill closes as the court decided; and a crew's wages left unpaid long enough bring the crew's claim against the
        /// company.
        /// </summary>
        public bool TestBills()
        {
            const string test = "bills";
            if (!CityReady(test))
            {
                return false;
            }
            try
            {
                BeforeTest();
                int day = ConstructionGame.Day;
                BillFile owed = NewBill(BillKind.Client, 0, 0, (long)Prices.At(2000), day - BillRules.SueAfterDays, day - BillRules.SueAfterDays);
                owed.PaysOn = BillRules.Never;
                string chased = Chase(owed);
                owed.PaysOn = BillRules.Never;   // the letter is not answered: to court
                string sued = SueFor(owed);
                bool heard = HearClaim(owed.Case);
                OwedDay(owed, day);   // the test's bills alone: the others wait for the day's pass as ever
                BillFile wages = NewBill(BillKind.Wages, 0, 0, (long)Prices.At(600), day - BillRules.WagesSueDays, day - BillRules.WagesSueDays);
                OwingDay(wages, day);
                bool court = CourtOpen();
                bool pass = chased == "reallife_bld_answer_chased" && (!court || (sued == "reallife_bld_answer_sued" && heard && (owed.Outcome == BillFile.Won || owed.Outcome == BillFile.Lost)
                            && wages.Outcome == BillFile.Sued && wages.Case > 0));
                Logger?.Info("Construction check (bills): the client's bill of " + owed.Amount + " chased (" + chased + "), taken to court (" + sued + ", claim " + owed.Case + "), heard and "
                             + (owed.Outcome == BillFile.Won ? "won" : owed.Outcome == BillFile.Lost ? "lost" : "not decided") + "; the crew's wages of " + wages.Amount + " left unpaid brought claim "
                             + wages.Case + ": " + (pass ? "PASS" : "FAIL") + ".");
                TestDone("a client taken to court for its bill, and a crew's claim for its wages", TabCity, owed.Id);
                return pass;
            }
            catch (Exception e)
            {
                Fault("a test (" + test + ")", e);
                return false;
            }
        }

        internal static bool CourtOpen()
        {
            try
            {
                return !Down("CourtOpen") && ConstructionBridges.CourtOpen != null && ConstructionBridges.CourtOpen();
            }
            catch (Exception e)
            {
                Faulty("CourtOpen", e);
                return false;
            }
        }

        internal static bool HearClaim(int number)
        {
            try
            {
                return number > 0 && !Down("HearClaim") && ConstructionBridges.HearClaim != null && ConstructionBridges.HearClaim(number);
            }
            catch (Exception e)
            {
                Faulty("HearClaim", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ the building of the year

        /// <summary>
        /// Test: the year's end brought forward: the buildings finished this year (and, for the test, one of the player's and
        /// the district's open lots as the developers' would be) given the critic's score, shortlisted, voted on for the vote's
        /// days and awarded now.
        /// </summary>
        public bool TestYearAward()
        {
            const string test = "building of the year";
            if (!CityReady(test))
            {
                return false;
            }
            if (!Settings.ConstructionCity)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return CityFail(test, "not started, the city around the company is off");
            }
            try
            {
                BeforeTest();
                int day = ConstructionGame.Day, dpy = ConstructionGame.DaysPerYear;
                AwardFile a = _state.Award;
                int year = AwardRules.Year(day, dpy);
                if (a.Year != year)
                {
                    a.Entries.Clear();
                    a.Voting = false;
                    a.Year = year;
                }
                bool mine = false;
                for (int i = 0; i < a.Entries.Count; i++)
                {
                    mine |= a.Entries[i].Builder == -1;
                }
                if (!mine)
                {
                    // One of the player's for the test: a building on a lot of theirs if they have one, else their office rebuilt.
                    Office(out PlaceView office);
                    string lot = _state.Lots.Count > 0 ? _state.Lots[0].Id : null;
                    NoteFinished(AwardEntry.OfPlayer, null, lot, lot != null ? null : office.Street ?? string.Empty, lot != null ? 0 : office.Number, lot != null ? BuildUse.Homes : BuildUse.Offices,
                        -1, 80, 0, day);
                }
                IReadOnlyList<LotView> lots = Lots;
                for (int i = 0, added = 0; i < lots.Count && a.Entries.Count < AwardRules.Shortlist + 1 && added < AwardRules.Shortlist; i++)
                {
                    BuildUse use = DevelopmentRules.Use(lots[i].Id, lots[i].Class);
                    int rival = DevelopmentRules.Rival(lots[i].Id, use);
                    NoteFinished(AwardEntry.OfDevelopers, null, lots[i].Id, lots[i].Street, lots[i].Number, use, rival, Math.Min(95, _state.Rivals[rival].Reputation + 10), 0, day);
                    added++;
                }
                int entries = a.Entries.Count;
                Shortlist(day);
                for (int k = 1; k < AwardRules.VoteDays(dpy); k++)
                {
                    Votes(day + k);
                }
                AwardDue(day);
                bool pass = entries > 0 && a.LastYear == year && a.Entries.Count == 0;
                Logger?.Info("Construction check (building of the year): " + entries + " building(s) of year " + year + " scored, shortlisted and voted on; the award to " + (a.LastYear == year
                    ? AwardName(a.Last) + " by builder " + a.Last.Builder + " with " + a.Last.Votes + " of " + a.LastVotes + " votes" : "nobody") + ": " + (pass ? "PASS" : "FAIL") + ".");
                TestDone("the building of the year awarded now", TabCity, 0);
                return pass;
            }
            catch (Exception e)
            {
                Fault("a test (" + test + ")", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ the put back

        // Before the state from before the tests comes back: the court's cases and claims the tests' rings and bills filed are
        // taken back unheard, as they came with what is being put back. Called by "put everything back".
        private int CityBack(BuildState back)
        {
            int withdrawn = 0;
            for (int i = 0; i < _state.Rings.Count; i++)
            {
                RingFile g = _state.Rings[i];
                RingFile before = back.Ring(g.Tender);
                if (g.Case > 0 && (before == null || before.Case != g.Case) && Withdraw(0, g.Case))
                {
                    withdrawn++;
                }
            }
            for (int i = 0; i < _state.Bills.Count; i++)
            {
                BillFile b = _state.Bills[i];
                BillFile before = back.Bill(b.Id);
                if (b.Case > 0 && (before == null || before.Case != b.Case) && Withdraw(b.Owed ? 2 : 1, b.Case))
                {
                    withdrawn++;
                }
            }
            return withdrawn;
        }

        // The council's last vote on an application, for the zoning test.
        private void NoteVote(int ayes, int noes, bool carried)
        {
            _lastAyes = ayes;
            _lastNoes = noes;
            _lastCarried = carried;
        }
    }
}
