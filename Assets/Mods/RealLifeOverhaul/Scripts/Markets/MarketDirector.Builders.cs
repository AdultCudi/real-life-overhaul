using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the market director: the city's builders on the market (BuilderMarket). Once a second the construction
    /// game's builder news goes onto the tape for the next moment, so a Going Public guest plays the host's; with the
    /// option off it is let go. The test entries play a piece of news now and can take it back. Nothing per frame.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private const string BuildersCheck = "Builders on the market check";

        private readonly List<BuilderMove> _builderTests = new List<BuilderMove>(16);
        private readonly List<MarketNews> _builderTestNews = new List<MarketNews>(4);
        private ulong _builderTestsOf;   // the market the moves were made in: another's are not taken back

        // Once a second, after the city's own news: the builders' onto the tape, a guest's dropped by the engine.
        private void BuildersSecond()
        {
            if (!Settings.MarketsBuilders)
            {
                BuilderMarket.Clear();
                return;
            }
            while (BuilderMarket.Take(out BuilderItem item))
            {
                _engine.Event(MarketInputs.EventBuilder, item.Rival, BuilderMarket.Packed(item.Kind, item.Weight));
            }
        }

        /// <summary>The listed company of a rival builder that is on the market now; null for a private one.</summary>
        public Stock BuilderStock(int rival)
        {
            if (!Running)
            {
                return null;
            }
            Stock s = _engine.State.Stock(BuilderMarket.StockOf(rival));
            return s != null && s.Listed ? s : null;
        }

        // ---------------------------------------------------------------- the test entries

        /// <summary>Test: a rival builder wins a public building's tender now; its price rises and every other listed builder's falls.</summary>
        public static void TestBuilderWins()
        {
            Instance?.BuilderTest("a rival builder wins a tender now", BuilderNewsKind.TenderWon, BuilderMarket.WeightMost);
        }

        /// <summary>Test: a rival builder's bribe to an inspector comes out now; its price and its usual margin fall.</summary>
        public static void TestBuilderScandal()
        {
            Instance?.BuilderTest("a bribery scandal at a rival builder now", BuilderNewsKind.Bribery, 1);
        }

        /// <summary>Test: what the builder tests did to the builders' prices taken back, and their news with it.</summary>
        public static void TestBuildersBack()
        {
            Instance?.BuildersBack();
        }

        private void BuilderTest(string what, BuilderNewsKind kind, int weight)
        {
            FinishMoment();
            string test = BuildersCheck + " (" + what + ")";
            if (!BuildersTestReady(test))
            {
                return;
            }
            if (!Settings.MarketsBuilders)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            int rival = TestBuilder(out Stock named);
            if (named == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_mkt_builders_test_none");
                return;
            }
            if (_builderTestsOf != State.Id)
            {
                _builderTests.Clear();
                _builderTestNews.Clear();
                _builderTestsOf = State.Id;
            }
            int before = named.MidCents;
            var others = new List<int>(BuilderMarket.Rivals);
            var othersBefore = new List<int>(BuilderMarket.Rivals);
            for (int r = 0; r < BuilderMarket.Rivals; r++)
            {
                Stock s = r != rival ? BuilderStock(r) : null;
                if (s != null)
                {
                    others.Add(s.Id);
                    othersBefore.Add(s.MidCents);
                }
            }
            int news = State.News.Count;
            int moved = _engine.BuilderNow(rival, kind, weight, _builderTests);
            for (int i = news; i < State.News.Count; i++)
            {
                _builderTestNews.Add(State.News[i]);
            }
            int after = named.MidCents;
            int fell = 0;
            for (int i = 0; i < others.Count; i++)
            {
                Stock s = State.Stock(others[i]);
                fell += s != null && s.MidCents < othersBefore[i] ? 1 : 0;
            }
            bool up = BuilderMarket.MoodBp(kind, weight, true, named.Size) > 0;
            bool othersRight = !BuilderMarket.MovesOthers(kind) || fell == others.Count;
            bool pass = moved > 0 && (up ? after > before : after < before) && othersRight && State.News.Count > news;
            Logger?.Info(test + ": " + named.Name + " (" + named.Ticker + ") " + MarketText.Price(before) + " to " + MarketText.Price(after) + " ("
                         + (before > 0 ? ((after - before) * 100.0 / before).ToString("+0.00;-0.00", CultureInfo.InvariantCulture) : "0") + "%, wanted " + (up ? "up" : "down") + ")"
                         + (BuilderMarket.MovesOthers(kind) ? ", " + fell + " of the " + others.Count + " other listed builder(s) down" : string.Empty) + ", its news in the market's: "
                         + (pass ? "PASS" : "FAIL"));
            Moved();
            OpenCompany(named.Id);
        }

        // A test moves prices at once, off the tape: never in a Going Public session, where the market is everyone's, and
        // a replay after a load need not be waited for (the tape keeps secrets and events, not prices).
        private bool BuildersTestReady(string test)
        {
            if (!TestReady(test, false))
            {
                return false;
            }
            if (GoingPublic.InSession)
            {
                TestRefusals.TellKey(Logger, test, "reallife_mkt_builders_test_session");
                return false;
            }
            return true;
        }

        // The builder a test moves: one the player holds, else one they watch, else Ostrander, the largest of the builders' own.
        private int TestBuilder(out Stock named)
        {
            MarketState s = State;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int r = 0; r < BuilderMarket.Rivals; r++)
                {
                    Stock k = BuilderStock(r);
                    if (k != null && k.BidCents == 0 && (pass == 0 ? s.Held(k.Id) != 0 : s.Watching(k.Id)))
                    {
                        named = k;
                        return r;
                    }
                }
            }
            for (int r = 0; r < BuilderMarket.Rivals; r++)
            {
                Stock k = BuilderStock(r);
                if (k != null && k.BidCents == 0 && BuilderMarket.OwnRow(k.Id) && k.Size == MarketSize.Mid)
                {
                    named = k;
                    return r;
                }
            }
            for (int r = 0; r < BuilderMarket.Rivals; r++)
            {
                Stock k = BuilderStock(r);
                if (k != null && k.BidCents == 0)
                {
                    named = k;
                    return r;
                }
            }
            named = null;
            return -1;
        }

        /// <summary>
        /// Test: how a company of the player's holding a construction business would come to the market: its trade's sector and
        /// its books over the last quarter with the construction company's own takings in them. Changes nothing.
        /// </summary>
        public static void TestListingBuilder()
        {
            Instance?.ListingBuilderTest();
        }

        private void ListingBuilderTest()
        {
            FinishMoment();
            const string test = "Listing check (a company holding a construction business)";
            if (!TestReady(test, false))
            {
                return;
            }
            int trade = MarketInputs.TradeOf(ConstructionTrade);
            MarketSector sector = SectorFor(trade);
            int day = Save.Day, from = day - Math.Max(1, Year / 4);
            OwnBooks books = MarketBridges.Books != null ? MarketBridges.Books(from, day) : default(OwnBooks);
            long own = MarketBridges.BuilderBooks != null ? MarketBridges.BuilderBooks(from, day) : -1L;
            bool pass = sector == MarketSector.Construction && (own < 0 || books.Revenue >= own);
            Logger?.Info(test + ": a construction company's trade (" + trade + ") is in the " + sector + " sector; the books of the last " + (day - from) + " days: takings "
                         + books.Revenue + ", profit " + books.Profit + " over " + books.Days + " day(s), " + (own >= 0 ? "the construction company's own takings " + own + " in them"
                             : "no construction company of the player's") + (Listing != null && Listing.Stage != ListingStage.None ? ", the company's worth " + Math.Round(Worth()) : string.Empty)
                         + ": " + (pass ? "PASS" : "FAIL"));
        }

        private const string ConstructionTrade = "reallife:businesstype_construction";   // the construction game's business type (Scripts/Construction)

        private void BuildersBack()
        {
            FinishMoment();
            string test = BuildersCheck + " (put the builders back)";
            if (!BuildersTestReady(test))
            {
                return;
            }
            if (_builderTests.Count == 0 || _builderTestsOf != State.Id)
            {
                TestRefusals.TellKey(Logger, test, "reallife_mkt_builders_test_nothing");
                return;
            }
            int undone = _engine.BuilderUndo(_builderTests);
            int removed = 0;
            for (int i = 0; i < _builderTestNews.Count; i++)
            {
                MarketNews n = _builderTestNews[i];
                for (int k = State.News.Count - 1; k >= 0; k--)
                {
                    MarketNews m = State.News[k];
                    if (m.Kind == n.Kind && m.Day == n.Day && m.Stock == n.Stock && m.A == n.A && m.B == n.B && m.C == n.C)
                    {
                        State.News.RemoveAt(k);
                        removed++;
                        break;
                    }
                }
            }
            int moves = _builderTests.Count;
            _builderTests.Clear();
            _builderTestNews.Clear();
            Logger?.Info(test + ": " + undone + " of " + moves + " move(s) taken back and " + removed + " piece(s) of news gone: " + (undone == moves ? "PASS" : "FAIL"));
            Moved();
        }
    }
}
