using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

// Startup exits (Scripts/Tech/Venture/ExitRules.cs, ExitPlaces.cs): an offer for the player's firm priced by revenue and growth, a sale paid through the firm account and the
// waterfall with the investors first, the way to the market (bankers, roadshow, pricing day, lock-up), the player's own shares sold at a later round, the soft landing, and the
// city's startups as real places (a founder who is a person of the city, a floor of a tower, one labour market). Every way of getting investors' money out through an exit is tried.
// Game-free; the sources are read for what only the game can do.
internal static partial class Program
{
    private static void StartupExitsChecks()
    {
        ExitOfferChecks();
        ExitSaleChecks();
        ExitSaleExploits();
        ExitListingChecks();
        ExitSecondaryChecks();
        ExitHireChecks();
        ExitFounderChecks();
        ExitFloorChecks();
        ExitPoolChecks();
        ExitOldSaveChecks();
        ExitWiringChecks();
    }

    private static List<ExitBuyer> ExitBuyers() => new List<ExitBuyer>
    {
        new ExitBuyer { Buyer = 0, Id = 3, Name = "Northgate Systems", Cash = long.MaxValue },
        new ExitBuyer { Buyer = 1, Id = 9, Name = "Pocketloop", Cash = 900000000 }
    };

    private static void ExitOfferChecks()
    {
        var firm = new Firm();
        long mrr = 5000000, arr = mrr * 12;
        long v0 = FirmsRules.Value(firm, arr, 0), vGrow = FirmsRules.Value(firm, arr, 1500), vBig = FirmsRules.Value(firm, arr * 2, 0);
        Check(vGrow > v0 && vBig > v0 * 19 / 10, "exits: the value an offer starts from rises with growth and with revenue (" + v0 + ", " + vGrow + ", " + vBig + ")");
        long listed = ExitRules.OfferPrice(v0, 1000, 0, 150), rival = ExitRules.OfferPrice(v0, 1000, 1, 150);
        Check(listed >= v0 * 11 / 10 && listed < v0 * 13 / 10 && rival >= v0 && rival < v0 * 1150 / 1000, "exits: a listed company pays 10 to 30 per cent over the firm's value, a startup up to 15 (" + listed + ", " + rival + ")");
        Check(ExitRules.OfferPrice(v0, 1250, 0, 7) > ExitRules.OfferPrice(v0, 1000, 0, 7) && ExitRules.OfferPrice(v0, 560, 0, 7) < ExitRules.OfferPrice(v0, 1000, 0, 7)
              && ExitRules.OfferPrice(vBig, 1000, 0, 7) > ExitRules.OfferPrice(v0, 1000, 0, 7) && ExitRules.OfferPrice(vGrow, 1000, 0, 7) > ExitRules.OfferPrice(v0, 1000, 0, 7),
            "exits: an offer's price rises with revenue, with growth and with the market's mood");
        Check(ExitRules.OfferChance(2000, 1000) > ExitRules.OfferChance(0, 1000) && ExitRules.OfferChance(99999, 1000) <= ExitCatalogue.OfferMost && ExitRules.OfferChance(0, 560) < ExitRules.OfferChance(0, 1250),
            "exits: offers come more often to a firm that grows and when the market is keen, never past the most");
        int flat = 0, growing = 0, tiny = 0, onTheWay = 0;
        List<ExitBuyer> buyers = ExitBuyers();
        for (int day = 1; day <= 3000; day++)
        {
            var f = new ExitFile { Seed = 4242 };
            flat += ExitRules.Offer(f, new FirmExit { Place = "Main~1" }, day, 1000, mrr, v0, 0, buyers) != null ? 1 : 0;
            growing += ExitRules.Offer(f, new FirmExit { Place = "Main~1" }, day, 1000, mrr, vGrow, 2500, buyers) != null ? 1 : 0;
            tiny += ExitRules.Offer(f, new FirmExit { Place = "Main~1" }, day, 1000, ExitCatalogue.OfferMrrLeast - 1, v0, 2500, buyers) != null ? 1 : 0;
            onTheWay += ExitRules.Offer(f, new FirmExit { Place = "Main~1", Stage = ListStage.Bankers }, day, 1000, mrr, v0, 2500, buyers) != null ? 1 : 0;
        }
        Check(growing > flat * 3 / 2 && flat > 0 && tiny == 0 && onTheWay == 0, "exits: over 3000 days a growing firm draws far more offers than a flat one (" + growing + " against " + flat + "), a tiny one or one on its way to the market none");
        var again = new ExitFile { Seed = 4242 };
        ExitOffer a = null, b = null;
        for (int day = 1; day < 2000 && a == null; day++)
        {
            a = ExitRules.Offer(new ExitFile { Seed = 4242 }, new FirmExit { Place = "Main~1" }, day, 1000, mrr, vGrow, 2500, buyers);
            b = ExitRules.Offer(again, new FirmExit { Place = "Main~1" }, day, 1000, mrr, vGrow, 2500, buyers);
        }
        Check(a != null && b != null && a.Price == b.Price && a.Name == b.Name && a.Made == b.Made, "exits: the same file and day draw the same offer");
        Check(a != null && a.Lapse == a.Made + ExitCatalogue.OfferDays && a.Kind == ExitRules.KindBuy, "exits: an offer is made on the day and stands " + ExitCatalogue.OfferDays + " days");
        // a startup offers only what it has
        var poor = new List<ExitBuyer> { new ExitBuyer { Buyer = 1, Id = 4, Name = "Thinpocket", Cash = 1234567 } };
        ExitOffer p = null;
        for (int day = 1; day < 4000 && p == null; day++)
        {
            p = ExitRules.Offer(new ExitFile { Seed = 11 }, new FirmExit { Place = "Main~1" }, day, 1250, mrr, vGrow, 2500, poor);
        }
        Check(p != null && p.Price <= 1234567, "exits: a startup of the city offers no more than it has (" + (p != null ? p.Price : -1) + ")");
        // lapse and counter
        var fe = new FirmExit { Place = "Main~1" };
        fe.Offers.Add(new ExitOffer { Id = 1, Name = "x", Price = 1000000, Made = 5, Lapse = 15 });
        Check(ExitRules.Lapse(fe, 15) == 0 && ExitRules.Lapse(fe, 16) == 1 && fe.Offers.Count == 0, "exits: an offer lapses after its last day");
        Check(ExitRules.CounterChance(0, 80, 80, 80) > ExitRules.CounterChance(2, 80, 80, 80) && ExitRules.CounterChance(1, 100, 100, 100) > ExitRules.CounterChance(1, 0, 0, 0)
              && ExitRules.CounterChance(0, 100, 100, 100) <= ExitCatalogue.CounterMost && ExitRules.CounterChance(2, 0, 0, 0) >= ExitCatalogue.CounterLeast,
            "exits: a counter is met more often with Selling, Analysis and Relationships, less for asking more, inside its bounds");
        int raised = 0, held = 0, walked = 0;
        long most = 0;
        for (int day = 0; day < 600; day++)
        {
            var o = new ExitOffer { Id = day + 1, Price = 1000000 };
            int r = ExitRules.Counter(new ExitFile { Seed = 3 }, o, 2, 60, 60, 60, day);
            raised += r == ExitRules.CounterRaised ? 1 : 0;
            held += r == ExitRules.CounterHeld ? 1 : 0;
            walked += r == ExitRules.CounterWalked ? 1 : 0;
            most = Math.Max(most, o.Price);
            Check(o.Countered, "exits: an offer countered is marked, whatever the buyer says");
            if (day > 3)
            {
                break;
            }
        }
        for (int day = 0; day < 600; day++)
        {
            var o = new ExitOffer { Id = day + 1, Price = 1000000 };
            int r = ExitRules.Counter(new ExitFile { Seed = 3 }, o, 2, 60, 60, 60, day);
            raised += r == ExitRules.CounterRaised ? 1 : 0;
            held += r == ExitRules.CounterHeld ? 1 : 0;
            walked += r == ExitRules.CounterWalked ? 1 : 0;
            most = Math.Max(most, o.Price);
        }
        Check(raised > 0 && held > 0 && walked > 0 && most == 1300000, "exits: a counter of a third is met, held or walked away from, and never pays more than a third over (" + raised + ", " + held + ", " + walked + ", " + most + ")");
        var once = new ExitOffer { Id = 5, Price = 1000000 };
        ExitRules.Counter(new ExitFile { Seed = 3 }, once, 0, 100, 100, 100, 1);
        long after = once.Price;
        Check(ExitRules.Counter(new ExitFile { Seed = 3 }, once, 2, 100, 100, 100, 2) == ExitRules.CounterHeld && once.Price == after, "exits: an offer can be countered once, a second counter changes nothing");
    }

    private static void ExitSaleChecks()
    {
        Cap c = FundedCap();
        long price = Raise * 3;
        SaleResult r = ExitRules.Settle(c, price, Raise, 20);
        long[] lines = VentureRules.Waterfall(c.Holders, price + StartupMoney.Balance(c));
        long owed = 0;
        for (int i = 0; i < c.Holders.Count; i++)
        {
            owed += c.Holders[i].Kind == (int)HolderKind.Vc ? lines[i] : 0;
        }
        Check(r.Owed == owed && r.Pot == price + Raise && r.Paid == r.Owed && r.Gap == 0 && r.Owed >= Raise,
            "exits: a sale pays the investors their lines of the waterfall (VentureRules.Waterfall as it stands) on the price and what the firm holds (" + r.Owed + " of " + r.Pot + ")");
        Check(r.Player == price - r.Paid && r.Player < price && r.Paid + r.Player == price, "exits: the investors are paid out of the price first; the player's gain is what is left (" + r.Player + ")");
        // money counted as taken is settled from the player's proceeds first, or stays owed
        Cap taken = FundedCap();
        StartupMoney.Shortfall(new List<Cap> { taken }, 0, Raise, 12, false, "ba:transaction_boatpurchase");
        StartupMoney.Wind noSale = StartupMoney.Close(taken, 0, 20);
        SaleResult sold = ExitRules.Settle(taken, price, 0, 20);
        Check(taken.Acc.Taken == Raise && noSale.Gap > 0 && sold.Gap == 0 && sold.Paid == sold.Owed && sold.Player == price - sold.Owed && sold.Player < price,
            "exits: investors' money counted as taken is paid from the price first (a close would have left a claim of " + noSale.Gap + ", the sale leaves " + sold.Gap + ")");
        SaleResult small = ExitRules.Settle(taken, Raise / 10, 0, 20);
        Check(small.Paid == Raise / 10 && small.Player == 0 && small.Gap == small.Owed - small.Paid && small.Gap > 0 && small.Fraud,
            "exits: a price too small to settle what was taken pays all of it to the investors, the player gets nothing, the rest stays owed as a claim and the pattern is fraud (" + small.Gap + ")");
        // a sale at nothing is the wind down
        Cap half = FundedCap();
        StartupMoney.Profit(half, -Raise * 4 / 10, 40);
        half.Bridge = Raise / 10;
        StartupMoney.Inflow(half, Raise / 10, 41);
        StartupMoney.Wind w = StartupMoney.Close(half, Raise / 2, 45);
        SaleResult zero = ExitRules.Settle(half, 0, Raise / 2, 45);
        Check(zero.Owed == w.Owed && zero.Paid == w.Paid && zero.Gap == w.Gap && zero.Bridge == w.Bridge, "exits: a sale for nothing is exactly the wind down: the same owed, paid and claim (" + w.Owed + ", " + w.Paid + ", " + w.Gap + ")");
        Check(ExitRules.Settle(half, price, Raise / 2, 45).Bridge == half.Bridge, "exits: the bridge loan is paid first in a sale as in a wind down");
        // a director's loan comes back into the pot
        Cap loan = FundedCap();
        long lent = StartupMoney.Lend(loan, 30);
        Cap noLoan = FundedCap();
        SaleResult withLoan = ExitRules.Settle(loan, price, Raise, 30), without = ExitRules.Settle(noLoan, price, Raise, 30);
        Check(lent > 0 && withLoan.Loan == lent && withLoan.Owed == without.Owed, "exits: a director's loan still out is returned to the pot by the sale, not forgiven (" + withLoan.Loan + ")");
        // the public a listing sold shares to is owed like any investor
        Cap ipo = VentureRules.NewCap("Main~1");
        var e = new FirmExit { Place = "Main~1", Fair = 400000000, Price = 400000000, Spread = 500, FloatBp = ExitCatalogue.FloatBp, Stage = ListStage.Roadshow };
        ExitRules.List(ipo, e, 100);
        long[] pl = VentureRules.Waterfall(ipo.Holders, StartupMoney.Balance(ipo) + price);
        long publicLine = 0;
        for (int i = 0; i < ipo.Holders.Count; i++)
        {
            publicLine += ipo.Holders[i].Kind == (int)HolderKind.Public ? pl[i] : 0;
        }
        SaleResult ipoSale = ExitRules.Settle(ipo, price, StartupMoney.Balance(ipo), 120);
        StartupMoney.Wind ipoClose = StartupMoney.Close(ipo, StartupMoney.Balance(ipo), 120);
        Check(publicLine > 0 && ipoSale.Owed == publicLine && ipoClose.Owed > 0 && StartupMoney.IsInvestor((int)HolderKind.Public),
            "exits: the public a listing sold shares to is paid its line of a sale and of a wind down like any investor (" + publicLine + ")");
        // no cap, no one to pay
        SaleResult own = ExitRules.Settle(null, price, 0, 20);
        SaleResult plain = ExitRules.Settle(VentureRules.NewCap("Main~1"), price, 0, 20);
        Check(own.Player == price && own.Owed == 0 && plain.Player == price && plain.Owed == 0 && plain.Gap == 0, "exits: a firm without investors pays the whole price to the player");
        // the staff pool is not an investor
        Cap pool = FundedCap();
        VentureRules.Grant(pool, VentureCatalogue.OptionGrant);
        long[] pooled = VentureRules.Waterfall(pool.Holders, price + StartupMoney.Balance(pool));
        long all = 0, poolLine = 0;
        for (int i = 0; i < pool.Holders.Count; i++)
        {
            all += pooled[i];
            poolLine += pool.Holders[i].Kind == (int)HolderKind.Pool ? pooled[i] : 0;
        }
        Check(poolLine > 0 && ExitRules.Settle(pool, price, Raise, 20).Owed + poolLine < all, "exits: a staff pool's line is not an investor's and is not paid to the venture firms");
    }

    private static void ExitSaleExploits()
    {
        var rnd = new Random(7);
        int bad = 0, gapped = 0, kept = 0;
        for (int n = 0; n < 600; n++)
        {
            long raise = (1 + rnd.Next(20)) * 10000000L;
            Cap c = FundedCap(rnd.Next(1, 7), raise, 10);
            long spent = raise * rnd.Next(0, 101) / 100;
            if (spent > 0 && rnd.Next(2) == 0)
            {
                StartupMoney.Shortfall(new List<Cap> { c }, 0, spent, 12, false, "ba:transaction_x");
            }
            else
            {
                StartupMoney.Profit(c, -spent, 12);
            }
            if (rnd.Next(5) == 0)
            {
                StartupMoney.Lend(c, 13);
            }
            long price = rnd.Next(0, 4) == 0 ? 0 : (long)(raise * (rnd.NextDouble() * 6));
            long wallet = rnd.Next(0, 3) == 0 ? 0 : (long)(raise * rnd.NextDouble() * 1.5);
            SaleResult r = ExitRules.Settle(c, price, wallet, 13);
            bool ok = r.Paid <= r.Owed && r.Paid == Math.Min(r.Owed, wallet + price) && r.Gap == r.Owed - r.Paid && r.Player == price - r.Paid && r.Gap >= 0
                      && (r.Gap == 0 || r.Player <= 0) && r.Player <= Math.Max(0, price - r.Owed) && wallet + r.Player >= 0;
            bad += ok ? 0 : 1;
            gapped += r.Gap > 0 ? 1 : 0;
            kept += r.Player > 0 ? 1 : 0;
            // a bigger price never leaves the investors with less
            bad += ExitRules.Settle(c, price + raise, wallet, 13).Owed >= r.Owed ? 0 : 1;
        }
        Check(bad == 0 && gapped > 20 && kept > 100, "exits: over 600 random sales the investors are paid first or are owed the rest, the player never keeps what they were owed, and a higher price never pays them less (" + gapped + " with a claim, " + kept + " with a gain)");
        // a sale cannot be a way to shed money counted as taken by cashing it out first
        Cap c1 = FundedCap();
        StartupMoney.Shortfall(new List<Cap> { c1 }, 0, Raise, 12, false, "ba:transaction_boatpurchase");
        SaleResult one = ExitRules.Settle(c1, Raise / 2, 0, 20);
        Check(one.Gap > 0 && one.Player <= 0, "exits: a boat bought with the raise and the firm sold for half of it leaves the investors a claim, not a free sale");
        // a counter cannot lift the price past a third; an offer is gone once sold or lapsed
        var fe = new FirmExit { Place = "Main~1" };
        string director = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(director.Contains("e.Offers.Clear();") && director.Contains("day > o.Lapse") && director.Contains("_venture.Caps.Remove(c);"),
            "exits: a sale clears the firm's offers, an offer past its day is refused, and the cap is removed so nothing stays dormant");
        int i1 = director.IndexOf("c.Acc.Wound = true;", StringComparison.Ordinal), i2 = director.IndexOf("ExitBridges.HandOver(place, o.Name)", StringComparison.Ordinal);
        int i3 = director.IndexOf("ExitRules.Settle(c, o.Price, Base(VentureGame.Cash), day)", StringComparison.Ordinal), i4 = director.IndexOf("VentureGame.Give(Level(r.Price), VentureGame.ExitKey", StringComparison.Ordinal);
        int i5 = director.IndexOf("VentureGame.Pay(Level(r.Paid), VentureGame.WindKey", StringComparison.Ordinal);
        Check(i1 > 0 && i2 > i1 && i3 > i2 && i4 > i3 && i5 > i4, "exits: the sale lifts the firm's floor, hands the business over, works the settlement out, takes the buyer's money and only then pays the investors");
        Check(director.Contains("VentureGame.Pay(Level(r.Paid), VentureGame.WindKey, name, null, true)") && !Regex.IsMatch(director, @"VentureGame\.Give\(Level\(r\.Player\)"),
            "exits: the player is never paid a settlement line: the wallet gets the price and the investors' lines come out of it");
        Check(director.Contains("TechBridges.InvestorClaim") && director.Contains("ShutDoors(day)") && director.Contains("TechBridges.InvestorFraud"),
            "exits: a sale that leaves a gap files the investors' claim, shuts the venture firms' doors and reports the clear pattern, as a wind down does");
        Check(fe.Offers.Count == 0, "exits: a firm has no offers before one is made");
        // the old wind down also pays the public (fails where it counted venture firms alone)
        Check(ModSource(@"Tech\Venture\FirmAccount.cs").Contains("w.Owed += IsInvestor(c.Holders[i].Kind) ? pay[i] : 0L;"), "exits: the wind down pays the public's lines as well as the venture firms'");
    }

    private static void ExitListingChecks()
    {
        var f = new ExitFile { Seed = 77 };
        FirmExit e = f.Exit("Main~1", true);
        long bar = ExitCatalogue.ListMrrLeast, fair = 600000000;
        Check(!ExitRules.ListReady(bar - 1, e) && ExitRules.ListReady(bar, e) && !ExitRules.Engage(e, bar - 1, 0, "Harbour City Bancorp", 0, fair, 100) && e.Stage == ListStage.None,
            "listing: a firm that takes less than a Series B asks for each month cannot start the way to the market");
        Check(!ExitRules.Hold(e, 1000, 50, 50, 1, 101), "listing: the roadshow cannot be held before the bankers are engaged");
        Check(ExitRules.Engage(e, bar, 0, "Harbour City Bancorp", 0, fair, 100) && e.Stage == ListStage.Bankers && e.Fee == ExitRules.Retainer(fair) && e.Fee >= ExitCatalogue.RetainerLeast && e.Fee <= ExitCatalogue.RetainerMost
              && e.FloatBp == ExitCatalogue.FloatBp, "listing: the bankers are engaged, their retainer set by the firm's worth inside its bounds (" + e.Fee + ")");
        Check(!ExitRules.Engage(e, bar, 1, "Other Bank", 1, fair, 101) && !ExitRules.ListReady(bar, e), "listing: a way under way cannot be started again");
        Check(ExitRules.BankReach(0, "A Bank") > ExitRules.BankReach(2, "A Bank") && ExitRules.BankSpread(900) > ExitRules.BankSpread(300) && ExitRules.BankSpread(0) == ExitCatalogue.SpreadBase,
            "listing: the largest bank reaches furthest and keeps most of what the offer raises");
        int mood = VentureRules.Mood(1);
        Check(ExitRules.Demand(mood, 80, 80, 800, 1) > ExitRules.Demand(mood, 80, 80, 800, 0) && ExitRules.Demand(mood, 80, 80, 800, 0) > ExitRules.Demand(mood, 80, 80, 800, 2)
              && ExitRules.Demand(mood, 90, 90, 800, 0) > ExitRules.Demand(mood, 10, 10, 800, 0) && ExitRules.Demand(VentureRules.Mood(0), 50, 50, 500, 0) > ExitRules.Demand(VentureRules.Mood(3), 50, 50, 500, 0)
              && ExitRules.Demand(mood, 50, 50, 900, 0) > ExitRules.Demand(mood, 50, 50, 100, 0) && ExitRules.Demand(mood, 100, 100, 1000, 1) <= 1000,
            "listing: the roadshow's demand rises with Selling, Relationships, the market's mood and the bank's reach, and with the deep way of holding it");
        Check(ExitRules.Hold(e, mood, 80, 80, 1, 110) && e.Stage == ListStage.Roadshow && e.PriceDay == 115 && e.Low > 0 && e.High > e.Low && e.Demand == ExitRules.Demand(mood, 80, 80, e.Reach, 1),
            "listing: the roadshow sets the demand and the range and fixes the pricing day five days on (" + e.Low + " to " + e.High + ")");
        Check(ExitRules.Price(f, e, 114) == ExitRules.Pricing.Waiting && e.Stage == ListStage.Roadshow, "listing: nothing is priced before the pricing day");
        Check(!ExitRules.Withdraw(new FirmExit()) && ExitRules.Withdraw(CopyOf(e)), "listing: a way under way can be withdrawn and one not started cannot");
        // pricing: inside the range, called off under the least the player takes
        int priced = 0, called = 0, outside = 0, calledHigh = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            var g = new ExitFile { Seed = seed };
            FirmExit x = g.Exit("Main~" + seed, true);
            ExitRules.Engage(x, bar, 0, "B", 0, fair, 100);
            ExitRules.Hold(x, mood, 0, 0, 2, 100);
            x.Demand = 0;
            int centre = ExitRules.Centre(0);
            ExitRules.Pricing p = ExitRules.Price(g, x, x.PriceDay);
            priced += p == ExitRules.Pricing.Priced ? 1 : 0;
            called += p == ExitRules.Pricing.CalledOff ? 1 : 0;
            outside += x.Price < fair * (centre - ExitCatalogue.RangeSpread) / 1000 || x.Price > fair * (centre + ExitCatalogue.RangeSpread) / 1000 ? 1 : 0;
            var h = new ExitFile { Seed = seed };
            FirmExit y = h.Exit("Main~" + seed, true);
            ExitRules.Engage(y, bar, 0, "B", 0, fair, 100);
            ExitRules.Hold(y, mood, 100, 100, 1, 100);
            y.Demand = 1000;
            calledHigh += ExitRules.Price(h, y, y.PriceDay) == ExitRules.Pricing.CalledOff ? 1 : 0;
        }
        Check(priced > 100 && called > 20 && outside == 0 && calledHigh == 0, "listing: the book is priced inside the range by the day's noise, called off for a price under the least the player takes, never with all the demand (" + priced + ", " + called + ")");
        // the listing: new shares for the public, money raised through the firm account, a lock-up
        var g2 = new ExitFile { Seed = 5 };
        FirmExit l = g2.Exit("Main~1", true);
        ExitRules.Engage(l, bar, 0, "Harbour City Bancorp", 0, fair, 100);
        ExitRules.Hold(l, mood, 80, 80, 1, 100);
        Check(ExitRules.Price(g2, l, 105) != ExitRules.Pricing.Waiting, "listing: the pricing day prices the book");
        l.Stage = ListStage.Roadshow;
        l.Price = fair;
        Cap c = VentureRules.NewCap("Main~1");
        long before = c.Total;
        long net = ExitRules.List(c, l, 105);
        Holder pub = c.Holders[c.Holders.Count - 1];
        Check(pub.Kind == (int)HolderKind.Public && pub.Shares == before * ExitCatalogue.FloatBp / (10000 - ExitCatalogue.FloatBp) && l.Gross > 0 && l.Net == net && net == l.Gross - l.Gross * l.Spread / 10000 && l.Stage == ListStage.Listed,
            "listing: the public buys a fifth of the company, the bank keeps its spread of the money raised (" + l.Gross + ", " + net + ")");
        Check(c.Acc.Open && c.Acc.Raised == net && StartupMoney.Floor(c, 106) == net && c.Post == l.Price + l.Gross, "listing: what reached the firm is money raised in the firm account, so the wallet must keep it (" + StartupMoney.Floor(c, 106) + ")");
        Check(l.LockTo == 105 + ExitCatalogue.LockUpDays && ExitCatalogue.LockUpDays == 180 && ExitRules.Locked(l, 105 + 179) && !ExitRules.Locked(l, 105 + 180) && !ExitRules.Locked(new FirmExit(), 5),
            "listing: the player's shares are locked up for 180 days after the listing");
        Check(ExitRules.Room(c, l, 105 + 100, 360) == 0 && ExitRules.Sell(c, l, 1, 20, 0, 105 + 100, 360) == 0, "listing: during the lock-up nothing of the player's shares can be sold, whatever the window");
        c.Acc.FounderDay = -5000;
        int day = l.LockTo;
        ExitRules.Round(c, l, day);
        Check(ExitRules.Window(l, day) && ExitRules.Room(c, l, day, 360) > 0 && ExitRules.Room(c, l, day + ExitCatalogue.QuarterDays + 1, 360) == 0,
            "listing: the lock-up over, a listed firm's shares may be sold for a quarter");
        // the old way is still reachable
        Check(ModSource(@"Tech\TechDirector.Exits.cs").Contains("ListWholeCompany") && ModSource(@"Ui\Tech\TechLive.Exits.cs").Contains("ListWholeCompany"),
            "listing: the whole company's own listing on the stock market stays one button away");
    }

    private static FirmExit CopyOf(FirmExit e)
    {
        return new FirmExit { Place = e.Place, Stage = e.Stage };
    }

    private static void ExitSecondaryChecks()
    {
        Cap c = FundedCap();
        var e = new FirmExit { Place = "Main~1" };
        ExitRules.Round(c, e, 10);
        Check(e.StageSeen == c.Stage && e.RoundDay == -1 && ExitRules.Room(c, e, 11, 360) == 0, "secondary: a firm seen for the first time opens no window: nothing is backdated");
        c.Acc.FounderDay = -5000;
        VentureRules.Accept(c, VentureRules.Sheet(6, 800000000, Raise, 700, 1000, 50), 500000, 50);
        ExitRules.Round(c, e, 50);
        long mine = ExitRules.PlayerShares(c);
        long room = ExitRules.Room(c, e, 55, 360);
        Check(e.RoundDay == 50 && room == mine * ExitCatalogue.SecondaryShare / 1000, "secondary: a round that closed opens the window; the player may sell a fifth of their vested shares (" + room + " of " + mine + ")");
        c.Acc.FounderDay = 50;
        Check(ExitRules.Room(c, e, 55, 360) == 0, "secondary: shares not vested are not for sale (inside the founder's cliff)");
        c.Acc.FounderDay = -5000;
        long floor = StartupMoney.Floor(c, 56), raised = c.Acc.Raised, balance = StartupMoney.Balance(c), own = c.Acc.Own, outs = c.Acc.Out;
        int discount = ExitRules.Discount(0, 0, false);
        long expect = ExitRules.SharePrice(c, room, discount);
        long paid = ExitRules.Sell(c, e, room, discount, 6, 56, 360);
        Check(paid == expect && paid > 0 && ExitRules.PlayerShares(c) == mine - room && e.Sold == room, "secondary: the shares go to the round's investors at the round's price less the discount (" + paid + ")");
        Check(c.Acc.Raised == raised && StartupMoney.Balance(c) == balance && c.Acc.Own == own && c.Acc.Out == outs && StartupMoney.Floor(c, 56) == floor && c.Acc.Taken == 0,
            "secondary: the money is the player's own shares': the firm's account, its raised money and the wallet's floor are as they were");
        Check(ExitRules.Sell(c, e, 1, discount, 6, 56, 360) == 0, "secondary: a window sells its share once and no more");
        Check(ExitRules.Room(c, e, 50 + ExitCatalogue.SecondaryDays + 1, 360) == 0, "secondary: the window shuts after " + ExitCatalogue.SecondaryDays + " days");
        Check(ExitRules.Discount(100, 100, false) < ExitRules.Discount(0, 0, false) && ExitRules.Discount(100, 100, false) >= ExitCatalogue.DiscountLeast && ExitRules.Discount(60, 60, true) == ExitCatalogue.QuoteSpread,
            "secondary: the player's Selling and Relationships take the discount down; a listed firm gives only the market's spread");
        long total = c.Holders.Count;
        Check(c.Holders.Count == total && c.Holders[c.Holders.Count - 1].Kind == (int)HolderKind.Vc && c.Holders[c.Holders.Count - 1].Preference == 0, "secondary: the buyers hold common shares, with no preference of their own");
        string director = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(director.Contains("VentureGame.Give(Level(paid), VentureGame.ShareSaleKey, firm.File.Name);") && !director.Contains("StartupMoney.Inflow(c, paid"),
            "secondary: the player is paid with no address and nothing is counted as raised");
        // a later sale of the firm still pays these buyers as holders, not the player's pocket
        Cap d = FundedCap();
        var f = new FirmExit { Place = "Main~1" };
        ExitRules.Round(d, f, 10);
        d.Acc.FounderDay = -5000;
        VentureRules.Accept(d, VentureRules.Sheet(6, 800000000, Raise, 700, 1000, 50), 500000, 50);
        ExitRules.Round(d, f, 50);
        ExitRules.Sell(d, f, ExitRules.Room(d, f, 51, 360), 150, 6, 51, 360);
        SaleResult r = ExitRules.Settle(d, Raise * 4, StartupMoney.Balance(d), 60);
        Check(r.Owed > ExitRules.Settle(FundedCap(), Raise * 4, Raise, 60).Owed / 2 && r.Paid == r.Owed, "secondary: shares sold early stay in the cap table and are paid with the investors in a sale");
    }

    private static void ExitHireChecks()
    {
        Cap c = FundedCap();
        long burn = Raise / 10 / 20;
        Check(!ExitRules.Failing(c, burn) && !ExitRules.Failing(c, 0) && !ExitRules.Failing(null, burn), "acqui-hire: a firm with money for a long time, or one that is not burning, or an unfunded one, is not failing");
        StartupMoney.Profit(c, -Raise * 9 / 10, 40);
        Check(ExitRules.Failing(c, burn) && ExitRules.HirePrice(c) == Raise * VentureCatalogue.AcquihireShare / 1000, "acqui-hire: a firm with under " + ExitCatalogue.FailingDays + " days of its account left is failing, and its team is worth the share of what was put in the city's acqui-hires pay");
        ExitOffer o = null;
        var f = new ExitFile { Seed = 31 };
        var e = new FirmExit { Place = "Main~1" };
        for (int day = 41; day < 400 && o == null; day++)
        {
            o = ExitRules.Hire(f, e, c, burn, day, ExitBuyers());
        }
        Check(o != null && o.Kind == ExitRules.KindHire && o.Hire && o.Price == ExitRules.HirePrice(c) && e.Offers.Count == 1, "acqui-hire: a failing firm is offered a small price for its team");
        Check(ExitRules.Hire(f, e, c, burn, 399, ExitBuyers()) == null, "acqui-hire: one offer for the team at a time");
        Check(ExitRules.Hire(new ExitFile { Seed = 31 }, new FirmExit { Place = "x" }, FundedCap(), burn, 50, ExitBuyers()) == null, "acqui-hire: a firm that is not failing is offered none");
        SaleResult r = ExitRules.Settle(c, o != null ? o.Price : 0, StartupMoney.Balance(c), 60);
        Check(r.Gap == 0 && !r.Fraud && r.Paid == r.Owed && r.Owed <= r.Pot && r.Player == r.Price - r.Paid, "acqui-hire: honest failure stays honest: nothing was taken, so the investors are paid what the waterfall gives and no claim follows (" + r.Owed + ")");
        Cap theft = FundedCap();
        StartupMoney.Shortfall(new List<Cap> { theft }, 0, Raise * 8 / 10, 20, true, "ba:transaction_yacht");
        StartupMoney.Profit(theft, -Raise * 1 / 10, 30);
        SaleResult t = ExitRules.Settle(theft, ExitRules.HirePrice(theft), 0, 60);
        Check(theft.Acc.Taken > 0 && t.Gap > 0 && t.Player <= 0, "acqui-hire: a firm that had its raise taken elsewhere cannot use the soft landing to be rid of the claim (" + t.Gap + ")");
        var unfunded = VentureRules.NewCap("Main~1");
        Check(ExitRules.HirePrice(unfunded) == 0 && ExitRules.HirePrice(null) == 0, "acqui-hire: a firm that raised nothing has no price for its team");
        string director = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(!director.Contains("Careers") && !director.Contains("JoinFirm"), "acqui-hire: the job offer for the player is left out (Careers has no way to hand the player an offer)");
    }

    private static PopulationState ExitCity()
    {
        PopulationState s = PopulationCity(out List<BusinessSeed> seeds);
        var sim = new PopulationSim();
        sim.BeginGenerate(s, 321, 1, 60, seeds);
        sim.RunAll();
        return s;
    }

    private static void ExitFounderChecks()
    {
        var file = new ExitFile { Seed = 91 };
        var s = new Startup { Id = 5, Name = "Pocketloop", Founder = "Sara Bell", Sector = (int)Venture.Fintech, Quality = 500, State = (int)StartupState.Running };
        FounderRec a = ExitPlaces.Invent(file, s, 10), b = ExitPlaces.Invent(new ExitFile { Seed = 91 }, s, 10);
        Check(!a.Linked && a.Age >= ExitCatalogue.FounderAgeLeast && a.Age <= ExitCatalogue.FounderAgeMost && a.Age == b.Age && a.Female == b.Female && a.Skill == 50 && a.Field == 4,
            "founders: with the population off the founder is invented as before, the same from the same file (age " + a.Age + ", skill " + a.Skill + ")");
        ExitBridges.Reset();
        Check(!ExitBridges.PeopleRun() && !ExitBridges.ResidentOf(5, out _), "founders: with the population off no resident is asked for and none is found");
        PopulationState city = ExitCity();
        bool anyTech = false;
        for (int p = 0; p < city.People.Count; p++)
        {
            anyTech |= city.People.Id[p] != 0 && city.People.Field[p] == 5 && city.People.Household[p] >= 0 && city.People.Female[p] == 0 && city.Age(p, 2) >= 30 && city.Age(p, 2) <= 40
                       && (city.People.Flags[p] & PersonFlags.Linked) == 0;
        }
        var asks = new List<LinkAsk> { new LinkAsk { Kind = LinkKind.Founder, Key = ExitBridges.KeyOf(7), Age = 35, Female = false, Prefer = 5 } };
        int made = PopulationLinks.People(city, asks, 2);
        bool found = PopulationLinks.Record(city, LinkKind.Founder, ExitBridges.KeyOf(7), 2, out int age, out bool female, out int field, out int skill);
        Check(made == 1 && found && !female && Math.Abs(age - 35) <= PopulationLinks.AgeGap * 3 && (field == 5 || !anyTech) && skill >= 0 && skill <= 100,
            "founders: with the population running a founder is a resident of their sex and age, from the field their startup works in where the city has one (field " + field + ", skill " + skill + ")");
        Check(PopulationLinks.People(city, asks, 3) == 0 && PopulationLinks.Linked(city, LinkKind.Founder, ExitBridges.KeyOf(7)) >= 0, "founders: the same resident stays the founder from day to day");
        PopulationState again = ExitCity();
        PopulationLinks.People(again, asks, 2);
        PopulationLinks.Record(again, LinkKind.Founder, ExitBridges.KeyOf(7), 2, out int age2, out _, out int field2, out int skill2);
        Check(age2 == age && field2 == field && skill2 == skill, "founders: the same city gives the same resident on every machine");
        asks.Clear();
        PopulationLinks.People(city, asks, 4);
        Check(PopulationLinks.Linked(city, LinkKind.Founder, ExitBridges.KeyOf(7)) < 0, "founders: a startup that is gone lets its founder go back to the city's people");
        // the founder takes the record: age, sex, skill, and the startup's quality follows it
        var rec = ExitPlaces.Invent(file, s, 10);
        int quality = ExitPlaces.Resident(rec, s, new ResidentInfo { Age = age, Female = female, Field = field, Skill = 90 });
        Check(rec.Linked && rec.Age == Math.Max(ExitCatalogue.FounderAgeLeast, age) && rec.Skill == 90 && rec.Female == female && quality == s.Quality && quality == (500 + ExitPlaces.QualityOf(90)) / 2
              && ExitPlaces.Resident(rec, s, new ResidentInfo { Age = 70, Skill = 0 }) == quality && rec.Skill == 90, "founders: the resident's record sets the founder's age, sex and skill once, and the startup's quality moves halfway to what that skill makes");
        // the bridge reads the record when asked and answers nothing when the population is off
        ExitBridges.Resident = (string key, out ResidentInfo info) => PopulationLinks.Record(again, LinkKind.Founder, key, 2, out int ag, out bool fe, out int fi, out int sk)
            ? Fill(out info, ag, fe, fi, sk) : Fill(out info, 0, false, 0, 0, false);
        ExitBridges.People = () => true;
        Check(ExitBridges.PeopleRun() && ExitBridges.ResidentOf(7, out ResidentInfo got) && got.Age == age2, "founders: the bridge gives the director the record of the resident behind a founder");
        ExitBridges.Reset();
        Check(!ExitBridges.PeopleRun() && !ExitBridges.ResidentOf(7, out _), "founders: put back, the population answers nothing and the founders stay invented");
        string places = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(Regex.IsMatch(places, @"if \(people && !r\.Linked && ExitBridges\.ResidentOf"), "founders: the day asks for a resident only while the population runs");
        Check(!ModSource(@"Tech\Venture\ExitPlaces.cs").Contains("Model") && !ModSource(@"Tech\Venture\ExitModel.cs").Contains("Face") && !ModSource(@"Tech\Venture\ExitPlaces.cs").Contains("Body"),
            "founders: only a record (name, age, skill) is kept; no model, face or body");
        Check(ModSource(@"Population\PopulationCatalogue.cs").Contains("Founder = 10"), "founders: the population knows a founder as a kind of person it links");
    }

    private static bool Fill(out ResidentInfo info, int age, bool female, int field, int skill, bool found = true)
    {
        info = new ResidentInfo { Age = age, Female = female, Field = field, Skill = skill };
        return found;
    }

    private static void ExitFloorChecks()
    {
        int towers = 3;
        var file = new ExitFile { Seed = 19 };
        var recs = new List<FounderRec>();
        for (int id = 1; id <= 20; id++)
        {
            var r = new FounderRec { Startup = id };
            file.Founders.Add(r);
            recs.Add(r);
            ExitPlaces.Let(file, r, towers);
        }
        var pairs = new HashSet<string>();
        int[] per = new int[towers];
        foreach (FounderRec r in recs)
        {
            pairs.Add(r.Tower + ":" + r.Slot);
            per[r.Tower]++;
        }
        Check(pairs.Count == 20 && Math.Max(per[0], Math.Max(per[1], per[2])) - Math.Min(per[0], Math.Min(per[1], per[2])) <= 1, "floors: twenty startups each let a place of their own, spread over the towers (" + per[0] + ", " + per[1] + ", " + per[2] + ")");
        var tenants = new List<StartupTenant>();
        foreach (FounderRec r in recs)
        {
            tenants.Add(new StartupTenant { Company = 12 + r.Startup, Name = "S" + r.Startup, Tower = r.Tower, Slot = r.Slot });
        }
        var floor = new int[tenants.Count];
        ExitPlaces.Floors(tenants, new[] { 4, 4, 4 }, floor);
        bool unique = true, above = true, none = false;
        for (int t = 0; t < towers; t++)
        {
            var seen = new List<int>();
            for (int i = 0; i < tenants.Count; i++)
            {
                if (tenants[i].Tower == t)
                {
                    seen.Add(floor[i]);
                    above &= floor[i] > 4 && floor[i] <= ExitCatalogue.TowerFloorsMost;
                    none |= floor[i] == 0;
                }
            }
            seen.Sort();
            for (int k = 0; k < seen.Count; k++)
            {
                unique &= seen[k] == 5 + k;
            }
        }
        Check(unique && above && !none, "floors: on each tower the startups hold the floors above the companies', one each, with no gap and none twice");
        // a startup gone frees its place for the next, the others stay put
        FounderRec gone = recs[3];
        int tower = gone.Tower, slot = gone.Slot;
        gone.Tower = -1;
        gone.Slot = 0;
        var next = new FounderRec { Startup = 99 };
        file.Founders.Add(next);
        ExitPlaces.Let(file, next, towers);
        var pairs2 = new HashSet<string>();
        foreach (FounderRec r in file.Founders)
        {
            if (r.Tower >= 0)
            {
                Check(pairs2.Add(r.Tower + ":" + r.Slot), "floors: no two startups hold one place (" + r.Tower + ":" + r.Slot + ")");
            }
        }
        int lowest = 1;
        foreach (FounderRec r in file.Founders)
        {
            if (!ReferenceEquals(r, next) && r.Tower == next.Tower && r.Slot == lowest)
            {
                lowest++;
                continue;
            }
        }
        bool free = true;
        for (int k = 1; k < next.Slot; k++)
        {
            bool held = false;
            foreach (FounderRec r in file.Founders)
            {
                held |= !ReferenceEquals(r, next) && r.Tower == next.Tower && r.Slot == k;
            }
            free &= held;
        }
        Check(free && tower >= 0 && slot >= 1, "floors: a startup is let the lowest place free on its tower, so the one a startup left is let again");
        // the same file lets the same floors; no room past what a building holds
        var f2 = new ExitFile { Seed = 19 };
        var again = new List<FounderRec>();
        for (int id = 1; id <= 20; id++)
        {
            var r = new FounderRec { Startup = id };
            f2.Founders.Add(r);
            again.Add(r);
            ExitPlaces.Let(f2, r, towers);
        }
        bool same = true;
        for (int i = 0; i < 20; i++)
        {
            same &= i == 3 || (again[i].Tower == recs[i].Tower && again[i].Slot == recs[i].Slot);   // (the fourth was let go above)
        }
        int differs = 0;
        for (int seed = 20; seed < 30; seed++)
        {
            var f3 = new ExitFile { Seed = seed };
            for (int id = 1; id <= 20; id++)
            {
                var r = new FounderRec { Startup = id };
                f3.Founders.Add(r);
                ExitPlaces.Let(f3, r, towers);
                differs += id != 4 && r.Tower != recs[id - 1].Tower ? 1 : 0;
            }
        }
        Check(same && differs > 0, "floors: the same file lets the same floors on every machine, another file lets others");
        var full = new List<StartupTenant>();
        for (int k = 1; k <= 6; k++)
        {
            full.Add(new StartupTenant { Company = 12 + k, Name = "F" + k, Tower = 0, Slot = k });
        }
        var fl = new int[full.Count];
        ExitPlaces.Floors(full, new[] { 9, 4 }, fl);
        Check(fl[0] == 10 && fl[1] == 11 && fl[2] == 12 && fl[3] == 0 && fl[5] == 0, "floors: no startup is given a floor past what a building holds (" + string.Join(",", fl) + ")");
        var none2 = new FounderRec { Startup = 1 };
        Check(!ExitPlaces.Let(new ExitFile { Seed = 1 }, none2, 0) && none2.Tower == -1, "floors: with no tower to let in a startup has no floor");
        string hq = ModSource(@"Tech\Sites\Headquarters.cs");
        Check(hq.Contains("PlaceStartups(stood)") && hq.Contains("ExitPlaces.Floors(") && hq.Contains("TechDirector.Instance.StartupTenants"), "floors: the headquarters put the city's startups on the floors above the companies' in each tower");
        Check(ModSource(@"Tech\TechDirector.Exits.cs").Contains("StartupTenants => _ventureOn && _exits != null"), "floors: with startups off the towers have no startup on them");
    }

    private static void ExitPoolChecks()
    {
        HiringFile h = NewLabour(77, 1);
        int open = HiringRules.Open(h);
        var file = new ExitFile { Seed = 9 };
        var s = new Startup { Id = 1, Name = "Pocketloop", Stage = (int)RoundStage.SeriesA, Burn = VentureCatalogue.BurnByStage[2], State = (int)StartupState.Running };
        FounderRec r = ExitPlaces.Invent(file, s, 1);
        long burn = s.Burn;
        int ask = ExitPlaces.MeanAsk(h);
        Check(open > 10 && ask > 0 && ExitPlaces.Target(burn, ask) > 1 && ExitPlaces.Target(burn * 4, ask) >= ExitPlaces.Target(burn, ask) && ExitPlaces.Target(0, ask) == 0 && ExitPlaces.Target(burn, 0) == 0,
            "pool: a startup wants people by what it burns over what a person asks (" + ExitPlaces.Target(burn, ask) + ")");
        // a seeker the player has offered a job to, or hired, is not for the startup to take
        Seeker held = null, hired = null;
        for (int i = 0; i < h.Seekers.Count; i++)
        {
            if (!h.Seekers[i].Taken && !h.Seekers[i].Passive)
            {
                if (held == null)
                {
                    held = h.Seekers[i];
                }
                else if (hired == null)
                {
                    hired = h.Seekers[i];
                }
            }
        }
        h.Applicants.Add(new Applicant { Id = 1, Seeker = held.Id, Ad = 0, Stage = (int)ApplyStage.Offered, Day = 1 });
        hired.Taken = true;   // the player's own hire marks the seeker taken
        var took = new List<Seeker>();
        for (int day = 2; day < 700 && took.Count < 12; day++)
        {
            Seeker k = ExitPlaces.Hire(h, file, r, s, r.Hired, burn, day);
            if (k != null)
            {
                took.Add(k);
            }
        }
        var ids = new HashSet<int>();
        bool clean = true;
        foreach (Seeker k in took)
        {
            clean &= ids.Add(k.Id) && k.Taken && k.Employer == s.Name && k.Id != held.Id && k.Id != hired.Id && !k.Passive;
        }
        Check(took.Count == 12 && clean && r.Hired == 12 && HiringRules.Open(h) == open - 12 - 1, "pool: a startup takes people from the labour market the player hires from, never one twice, never one the player holds an offer to or has hired (" + took.Count + ")");
        // a second startup finds them gone
        var s2 = new Startup { Id = 2, Name = "Dashnest", Stage = (int)RoundStage.SeriesA, Burn = burn, State = (int)StartupState.Running };
        FounderRec r2 = ExitPlaces.Invent(file, s2, 1);
        bool overlap = false;
        for (int day = 800; day < 1600 && r2.Hired < 8; day++)
        {
            Seeker k = ExitPlaces.Hire(h, file, r2, s2, r2.Hired, burn, day);
            overlap |= k != null && ids.Contains(k.Id);
        }
        Check(!overlap && r2.Hired > 0, "pool: a hire one startup makes is gone for the next, and the player's list loses it");
        // the player's application to a seeker a startup took is withdrawn by the market's own day
        var notes = new List<MarketNote>();
        h.Applicants.Add(new Applicant { Id = 2, Seeker = took[0].Id, Ad = 0, Stage = (int)ApplyStage.New, Day = 1 });
        HiringRules.Day(h, 801, 1, false, HiringCatalogue.RivalFirms, FlatRate, new[] { "d1" }, notes);
        Applicant app = h.ApplicantOf(2);
        Check(app == null || app.Stage == (int)ApplyStage.Withdrawn, "pool: an application of the player's to someone a startup took is withdrawn, as it is for a rival's hire");
        // stops at its target
        var s3 = new Startup { Id = 3, Name = "Tinyloop", Stage = (int)RoundStage.PreSeed, Burn = VentureCatalogue.BurnByStage[0], State = (int)StartupState.Running };
        FounderRec r3 = ExitPlaces.Invent(file, s3, 1);
        int target = ExitPlaces.Target(s3.Burn, ExitPlaces.MeanAsk(h));
        for (int day = 900; day < 4000; day++)
        {
            ExitPlaces.Hire(h, file, r3, s3, r3.Hired, s3.Burn, day);
        }
        Check(r3.Hired == target || HiringRules.Open(h) == 0, "pool: a startup stops hiring at the people it wants (" + r3.Hired + " of " + target + ")");
        string places = ModSource(@"Tech\TechDirector.Exits.cs");
        Check(Regex.IsMatch(places, @"if \(_hiringOn && _hiring != null\)[\s\S]{0,120}ExitPlaces\.Hire\(_hiring"), "pool: a startup hires only while the player's labour market runs");
    }

    private static void ExitOldSaveChecks()
    {
        Check(ExitStore.Load(new Dictionary<string, string>(), out _) == null && ExitStore.Key == "reallife:tech_exits" && VentureStore.Key == "reallife:tech_ventures" && VentureStore.Version == 2,
            "old saves: a save from before has no exits line, the venture line is as it was (version 2) and exits have a line of their own");
        // a running funded firm with no exits line: nothing standing, no window, no offer made for the past
        Cap c = FundedCap();
        var file = new ExitFile { Seed = 5 };
        FirmExit e = file.Exit("Main~1", true);
        ExitRules.Round(c, e, 500);
        Check(e.Offers.Count == 0 && file.NextOffer == 1 && e.RoundDay == -1 && e.Stage == ListStage.None && e.SoldDay == 0 && ExitRules.Room(c, e, 501, 360) == 0,
            "old saves: a running firm loaded with no exits has no offer, no window and no sale, and no one is paid for the past");
        int offers = 0;
        for (int day = 500; day < 503; day++)
        {
            ExitRules.Offer(file, e, day, 1000, 9000000, 400000000, 2500, ExitBuyers());
        }
        offers = e.Offers.Count;
        Check(offers <= ExitCatalogue.OffersMost && (offers == 0 || e.Offers[0].Made >= 500), "old saves: an offer can only be made on a day from now on, never dated before the save was loaded");
        // the city's startups seen for the first time get a founder and a floor
        var pool = new ExitFile { Seed = 8 };
        var startups = new List<Startup>();
        for (int i = 1; i <= 6; i++)
        {
            startups.Add(new Startup { Id = i, Name = "S" + i, Founder = "F" + i, Sector = i % 9, Quality = 100 * i, State = (int)StartupState.Running });
        }
        foreach (Startup s in startups)
        {
            FounderRec r = pool.FounderOf(s.Id) ?? ExitPlaces.Invent(pool, s, 700);
            if (pool.FounderOf(s.Id) == null)
            {
                pool.Founders.Add(r);
                ExitPlaces.Let(pool, r, 3);
            }
        }
        Check(pool.Founders.Count == 6 && pool.Founders.TrueForAll(x => x.Tower >= 0 && x.Slot >= 1 && x.Seen == 700), "old saves: startups already in the city get a founder and a floor on the first day they are seen");
        // the line round trips, and a reader that knows less ignores what it does not know
        e.Stage = ListStage.Listed;
        e.BankName = "Harbour City Bancorp";
        e.LockTo = 777;
        e.Offers.Add(new ExitOffer { Id = 4, Kind = 1, Buyer = 1, BuyerId = 9, Name = "Pocketloop", Price = 123456, Made = 700, Lapse = 710, Countered = true });
        pool.Founders[0].Linked = true;
        pool.Founders[0].Hired = 3;
        string line = ExitStore.Write(pool.Founders.Count > 0 ? Merge(file, pool) : file);
        ExitFile back = ExitStore.Read(line, out int version);
        FirmExit be = back.Exit("Main~1", false);
        Check(version == ExitStore.Version && be != null && be.Stage == ListStage.Listed && be.LockTo == 777 && be.BankName == "Harbour City Bancorp" && be.OfferOf(4) != null && be.OfferOf(4).Price == 123456 && be.OfferOf(4).Countered
              && back.Founders.Count == 6 && back.Founders[0].Linked && back.Founders[0].Hired == 3 && back.Seed == 5 && back.NextOffer == 5, "old saves: the exits line comes back as it was written");
        ExitFile extra = ExitStore.Read(line + "~z:1|2|3;4", out _);
        Check(extra.Founders.Count == 6 && extra.Firms.Count == back.Firms.Count, "old saves: a part this reader does not know is ignored");
        var vf = new VentureFile { Seed = 3 };
        Cap pub = VentureRules.NewCap("Main~2");
        pub.Holders.Add(new Holder { Kind = (int)HolderKind.Public, Shares = 5, Invested = 9 });
        vf.Caps.Add(pub);
        VentureFile vback = VentureStore.Read(VentureStore.Write(vf), out _);
        Check(vback.Caps[0].Holders.Count == 2 && vback.Caps[0].Holders[1].Kind == (int)HolderKind.Public && !VentureStore.Write(vf).Contains("tech_exits"), "old saves: the public's line rides in the venture line's own holders, whose fields are as they were");
        Check(ExitStore.Load(new Dictionary<string, string> { { ExitStore.Key, "" } }, out _) == null, "old saves: an empty line reads as nothing");
    }

    private static ExitFile Merge(ExitFile a, ExitFile b)
    {
        foreach (FounderRec r in b.Founders)
        {
            a.Founders.Add(r);
        }
        a.NextOffer = Math.Max(a.NextOffer, 5);
        return a;
    }

    private static void ExitWiringChecks()
    {
        string venture = ModSource(@"Tech\TechDirector.Venture.cs"), exits = ModSource(@"Tech\TechDirector.Exits.cs"), money = ModSource(@"Tech\TechDirector.Money.cs");
        Check(Regex.IsMatch(venture, @"private void StartVenture\(\)[\s\S]*?StartExits\(\);") && Regex.IsMatch(venture, @"CapsDay\(day, mood\);\s*ExitsDay\(day\);") && venture.Contains("StopExits(keep);") && venture.Contains("WriteExits(data);"),
            "options off: exits start, run on the day, save and stop with the venture part, so with Startups off none of it runs");
        Check(exits.Contains("public ExitFile Exits => _ventureOn ? _exits : null;") && !Regex.IsMatch(exits, @"void (Update|LateUpdate|FixedUpdate)\("), "options off: the file is none while startups are off, and nothing runs every frame");
        Check(exits.Contains("if (_exits == null || _exits.LastDay >= day)"), "cost: the day's work runs once a day");
        Check(!Regex.IsMatch(exits, @"GoingPublic|SharedCity|Network") && !ModSource(@"Wiring\Wiring.ExitLinks.cs").Contains("GoingPublic"), "going public: nothing here is sent: the file is the host's own, seeded, and an older RLO never reads the line");
        Check(ModSource(@"Wiring\Wiring.SectorLinks.cs").Contains("ExitLinks.Connect()") && ModSource(@"Wiring\Wiring.PopulationLinks.cs").Contains("ExitLinks.Founders(into)"), "wiring: the links are connected with the venture sessions and the founders asked of the population");
        Check(ModSource(@"Ui\Tech\TechLive.Venture.cs").Contains("ExitsPage(picked)") && ModSource(@"Ui\Tech\TechLive.Venture.cs").Contains("StartupPlace(s)") && ModSource(@"Ui\Tech\TechModel.cs").Contains("\"exits:\""),
            "wiring: the cap table links to the page of exits, a startup's page to its founder and office, and a notice lands on the page");
        Check(ModSource(@"Economy\BizLines.cs").Contains("tech_venture_exit") && ModSource(@"Economy\BizLines.cs").Contains("tech_listing_bankers"), "wiring: the sale's money and the bankers' retainer are booked as capital and as a cost of the firm");
        Check(money.Contains("StartupMoney") && ModSource(@"Tech\TechDirector.Deals.cs").Contains("OpenExits(place)"), "wiring: Ask for a listing opens the firm's page of exits");
        // the pages build in every state and every button of them reaches its run
        int pressed = 0, wrong = 0;
        foreach (string state in new[] { "exits", "exits_market", "exits_listed", "exits_hire", "startup_place" })
        {
            TechModel m = UiSamples.Tech((key, english) => english, state);
            Check(Array.IndexOf(UiSamples.TechStates, state) >= 0 && m.Shape == TechShape.Sheet && m.Runs.Length > 0, "exits (" + state + "): the page is among the Tech states and builds with something to press");
            foreach (UiFactCardModel card in new List<UiFactCardModel>(m.Left) { }.Count > 0 ? Cards(m) : new List<UiFactCardModel>())
            {
                foreach (UiActionModel act in card.Actions)
                {
                    pressed++;
                    wrong += act.Arg >= 0 && act.Arg < m.Runs.Length ? 0 : 1;
                }
            }
        }
        Check(pressed >= 10 && wrong == 0, "exits: every button of the pages carries a run of its own (" + pressed + ")");
    }

    private static List<UiFactCardModel> Cards(TechModel m)
    {
        var all = new List<UiFactCardModel>();
        all.AddRange(m.Left);
        all.AddRange(m.Right);
        return all;
    }
}
