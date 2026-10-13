using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;

// The car dealership (Scripts/Economy/Dealer): what a car is worth, the lot, the market (new cars, used ones with their
// faults and haggling, the trade auction), customers and their offers, the dealership's own finance and warranties, what
// each transaction is to the books, the save's line, and a month of a reference dealership measured against the bound the
// catalogue states.
internal static partial class Program
{
    private static readonly MarketModel[] DealerModels =
    {
        new MarketModel("ba:vehicletype_honzamimic", 9000f), new MarketModel("ba:vehicletype_bima320", 32000f), new MarketModel("ba:vehicletype_mersaidis500", 85000f),
        new MarketModel("ba:vehicletype_vordpony", 45000f), new MarketModel("ba:vehicletype_umcnunavut", 52000f)
    };

    private static readonly string[] DealerColours = { "Red", "Black", "Silver" };

    private static void Dealer()
    {
        DealerWorth();
        DealerLot();
        DealerMarket();
        DealerAuction();
        DealerCustomers();
        DealerCredit();
        DealerBooks();
        DealerSave();
        DealerBalance();
    }

    private static void DealerWorth()
    {
        Check(DealerRules.Worth(30000f, 1000, 0, 1000) == 30000 && DealerRules.Worth(30000f, 1200, 0, 1000) == 36000,
            "dealer: a new car is worth its list price at the city's price level");
        Check(DealerRules.Worth(30000f, 1000, 90000, 1000) == 18000 && DealerRules.Worth(30000f, 1000, 0, 700) == 24000,
            "dealer: kilometres and condition take from a car's worth by the catalogue's points (got " + DealerRules.Worth(30000f, 1000, 90000, 1000) + ", " + DealerRules.Worth(30000f, 1000, 0, 700) + ")");
        bool falls = true;
        for (int km = 0; km < 400000; km += 5000)
        {
            falls &= DealerRules.WorthShare(km + 5000, 800) <= DealerRules.WorthShare(km, 800);
        }
        for (int c = 0; c < 1000; c += 50)
        {
            falls &= DealerRules.WorthShare(40000, c) <= DealerRules.WorthShare(40000, c + 50);
        }
        Check(falls && DealerRules.WorthShare(900000, 0) == DealerCatalogue.WorthLeast,
            "dealer: a car is worth less with every kilometre and every point of condition lost, never under the least");
        Check(DealerRules.Asking(20000) == 21000 && DealerRules.Asking(20010) % DealerCatalogue.Round == 0 && DealerRules.TradePrice(20000) == 15600,
            "dealer: a car is asked at the mark-up, named in fifties, and the trade pays its share");
        bool steps = true;
        for (int s = 0; s < DealerRules.AskSteps; s++)
        {
            steps &= DealerRules.AskStepOf(25000, DealerRules.AskAt(25000, s)) == s && (s == 0 || DealerRules.AskAt(25000, s) > DealerRules.AskAt(25000, s - 1));
        }
        Check(steps && DealerRules.AskAt(25000, 0) == 20000 && DealerRules.AskAt(25000, DealerRules.AskSteps - 1) == 35000,
            "dealer: the asking prices the player steps through rise from 80% to 140% of the worth, and a price finds its own step");
        int known = (1 << 0) | (1 << 4);
        Check(DealerRules.FaultCondition(known) == 130 && DealerRules.FaultCount(known) == 2
              && DealerRules.PrepareCost(30000, 800, known, 120) == DealerCatalogue.Named(120 + 1800 + 180 + 1200)
              && DealerRules.Prepared(700, known, known) == 941
              && DealerRules.Prepared(990, 0, 0) == 1000,
            "dealer: preparing a car costs a valet, the missing condition's share and each known fault, and puts most of the condition back (got "
            + DealerRules.PrepareCost(30000, 800, known, 120) + ", " + DealerRules.Prepared(700, known, known) + ")");
    }

    private static void DealerLot()
    {
        var into = new List<int>();
        bool across;
        int small = DealerRules.Layout(800, 900, into, out across);
        Check(small == DealerCatalogue.SpotsLeast && into.Count == 2 * small, "dealer: a tiny floor still has the least places (got " + small + ")");
        int big = DealerRules.Layout(5000, 6000, into, out across);
        Check(big == DealerCatalogue.SpotsMost, "dealer: a huge floor has the most places and no more (got " + big + ")");
        int mid = DealerRules.Layout(1800, 2600, into, out across);
        bool inside = true, apart = true;
        for (int i = 0; i < mid; i++)
        {
            int x = into[2 * i], z = into[2 * i + 1];
            inside &= Math.Abs(x) <= 900 - DealerCatalogue.WallGap && Math.Abs(z) <= 1300 - DealerCatalogue.WallGap;
            for (int j = 0; j < i; j++)
            {
                int dx = Math.Abs(x - into[2 * j]), dz = Math.Abs(z - into[2 * j + 1]);
                apart &= dx >= DealerCatalogue.SpotWidth || dz >= DealerCatalogue.SpotDepth;
            }
        }
        Check(mid == 12 && !across && inside && apart,
            "dealer: a shop floor of 18 x 26 m holds rows of cars inside its walls, none on another (got " + mid + " places)");
        DealerRules.Layout(2600, 1800, into, out across);
        Check(across, "dealer: on a floor wider than it is deep the cars face across it");
    }

    private static void DealerMarket()
    {
        var s = new DealerState { Seed = 1234 };
        DealerRules.Morning(s, 10, DealerModels, DealerColours, 1000);
        string first = DealerStore.Write(s);
        var again = new DealerState { Seed = 1234 };
        DealerRules.Morning(again, 10, DealerModels, DealerColours, 1000);
        Check(first == DealerStore.Write(again) && s.Market.Count == DealerCatalogue.WholesaleADay + DealerCatalogue.UsedADay,
            "dealer: a day's market is the same however often it is drawn, with its new and its used cars");
        bool priced = true, floors = true, shown = true;
        int faulty = 0;
        for (int i = 0; i < s.Market.Count; i++)
        {
            DealerListing l = s.Market[i];
            int worthNew = DealerRules.Worth(Model(l.Type).Price, 1000, 0, 1000);
            if (l.Kind == ListingKind.Wholesale)
            {
                priced &= l.Asking >= DealerCatalogue.Share(worthNew, DealerCatalogue.WholesaleLeast) - DealerCatalogue.Round && l.Asking <= DealerCatalogue.Share(worthNew, DealerCatalogue.WholesaleMost) + DealerCatalogue.Round
                          && l.Condition == 1000 && l.Faults == 0 && l.Km <= DealerCatalogue.WholesaleKmMost;
            }
            else
            {
                floors &= l.Floor <= l.Asking && l.Floor > 0;
                shown &= l.Shown >= l.Condition && (l.Faults != 0 || l.Shown == l.Condition);
                faulty += l.Faults != 0 ? 1 : 0;
            }
        }
        Check(priced, "dealer: new cars are listed at the wholesaler's share of their worth, as new");
        Check(floors && shown, "dealer: an owner's least is under their asking price, and an owner with a fault to hide says the car is better than it is");

        // Over many days: faults come with wear, an inspection finds them and brings the least down.
        int seen = 0, withFaults = 0, lowered = 0;
        long worn = 0, fresh = 0;
        int wornCars = 0, freshCars = 0;
        for (int day = 0; day < 300; day++)
        {
            var d = new DealerState { Seed = 99 };
            DealerRules.Morning(d, day, DealerModels, DealerColours, 1000);
            for (int i = 0; i < d.Market.Count; i++)
            {
                DealerListing l = d.Market[i];
                if (l.Kind != ListingKind.Used)
                {
                    continue;
                }
                seen++;
                if (l.Faults != 0)
                {
                    withFaults++;
                    int before = l.Floor;
                    DealerRules.Inspect(l);
                    lowered += l.Floor < before && l.Inspected ? 1 : 0;
                }
                if (l.Km > 120000)
                {
                    worn += DealerRules.FaultCount(l.Faults);
                    wornCars++;
                }
                else if (l.Km < 35000)
                {
                    fresh += DealerRules.FaultCount(l.Faults);
                    freshCars++;
                }
            }
        }
        Check(seen == 300 * DealerCatalogue.UsedADay && withFaults > seen / 5 && withFaults < seen * 9 / 10 && lowered == withFaults,
            "dealer: some used cars hide faults and every inspection of one lowers the owner's least (" + withFaults + " of " + seen + ")");
        Check(wornCars > 0 && freshCars > 0 && worn * freshCars > fresh * wornCars * 2,
            "dealer: a car with many kilometres hides more faults than a fresh one (" + worn + " in " + wornCars + " against " + fresh + " in " + freshCars + ")");
        Check(DealerRules.InspectionFee(10000, 150, 1200) == 150 && DealerRules.InspectionFee(50000, 150, 1200) == 500 && DealerRules.InspectionFee(900000, 150, 1200) == 1200,
            "dealer: an inspection costs a share of the car's worth within its least and most");

        var owner = new DealerListing { Kind = ListingKind.Used, Asking = 20000, Floor = 18600 };
        int counter;
        Check(DealerRules.Haggle(owner, 18600, out counter) == HaggleAnswer.Taken, "dealer: an offer at the owner's least is taken");
        HaggleAnswer a1 = DealerRules.Haggle(owner, 16000, out int c1);
        HaggleAnswer a2 = DealerRules.Haggle(owner, 16000, out int c2);
        HaggleAnswer a3 = DealerRules.Haggle(owner, 16000, out int c3);
        Check(a1 == HaggleAnswer.Countered && c1 == 18950 && a2 == HaggleAnswer.Countered && c2 <= c1 && a3 == HaggleAnswer.WalkedAway && owner.Gone,
            "dealer: under the least the owner counters with their least and a quarter of the gap, and walks away at the third offer (got " + c1 + ")");
        var owner2 = new DealerListing { Kind = ListingKind.Used, Asking = 20000, Floor = 18600 };
        DealerRules.Haggle(owner2, 15000, out int c4);
        Check(DealerRules.Haggle(owner2, c4, out counter) == HaggleAnswer.Taken, "dealer: the owner's own counter, offered back, is taken");
        Check(DealerRules.HaggleAt(20000, 0) == 15000 && DealerRules.HaggleAt(20000, DealerRules.HaggleSteps - 1) == 20000, "dealer: the player's offers to an owner run from 75% to all of the asking price");
    }

    private static MarketModel Model(string type)
    {
        for (int i = 0; i < DealerModels.Length; i++)
        {
            if (DealerModels[i].Type == type)
            {
                return DealerModels[i];
            }
        }
        return DealerModels[0];
    }

    private static void DealerAuction()
    {
        var s = new DealerState { Seed = 7 };
        int now = 30 * 24 + 9;
        Check(DealerRules.SaleDue(s, 9) && !DealerRules.SaleDue(s, 3) && !DealerRules.SaleDue(s, 22), "dealer: a sale opens only in the auction's hours");
        DealerRules.OpenSale(s, now, DealerModels, DealerColours, 1000);
        bool ends = s.Market.Count == DealerCatalogue.AuctionLots, opening = true;
        for (int i = 0; i < s.Market.Count; i++)
        {
            DealerListing l = s.Market[i];
            int worth = DealerRules.WorthOf(l, DealerModels, 1000);
            ends &= l.Ends > now && l.Leader == -1 && l.Kind == ListingKind.Auction;
            opening &= l.Asking == DealerCatalogue.Named(DealerCatalogue.Share(worth, DealerCatalogue.OpeningShare)) && l.Ceiling >= l.Asking && Math.Abs(l.Shown - l.Condition) <= DealerCatalogue.AuctionShownSpread;
        }
        Check(ends && opening && !DealerRules.SaleDue(s, 10), "dealer: a sale's lots open at their share of their worth and run a few hours, and no second sale opens meanwhile");
        DealerListing lot = s.Market[0];
        int lotWorth = DealerRules.WorthOf(lot, DealerModels, 1000);
        int opened = lot.Asking;
        Check(DealerRules.Bid(lot, lotWorth, now) && lot.Leader == 0 && lot.Asking == opened && !DealerRules.Bid(lot, lotWorth, now),
            "dealer: the first bid is the opening one, and a leader cannot bid against themselves");
        bool under = true;
        int outbid = 0;
        for (int h = 1; h < 8; h++)
        {
            outbid += DealerRules.RoomHour(s, now + h, DealerModels, 1000);
            for (int i = 0; i < s.Market.Count; i++)
            {
                under &= s.Market[i].Asking <= Math.Max(s.Market[i].Ceiling, opened);
            }
            if (lot.Leader > 0 && now + h < lot.Ends)
            {
                int was = lot.Asking;
                DealerRules.Bid(lot, lotWorth, now + h);
                under &= lot.Asking == was + DealerRules.Increment(lotWorth);
            }
        }
        var down = new List<DealerListing>();
        DealerRules.KnockedDown(s, now + 8, down);
        Check(under && down.Count == DealerCatalogue.AuctionLots, "dealer: the room never bids past its ceiling, a bid is a step over the last, and every lot is knocked down at its hour (outbid " + outbid + " times)");
        Check(DealerRules.Increment(1000) == DealerCatalogue.IncrementLeast && DealerRules.Increment(10000000) == DealerCatalogue.IncrementMost && DealerRules.BuyersFee(1000, 100) == 100 && DealerRules.BuyersFee(40000, 100) == 2000,
            "dealer: the steps of the bidding and the buyer's fee are within their bounds");
    }

    private static void DealerCustomers()
    {
        var hour = new YardHour { Open = true, ShopCustomers = 25f, Promotion = 50, BaseMultiplier = 0.5f, Satisfaction = 50, Staff = 2, Skill = 50 };
        long t50 = DealerRules.VisitorThousandths(hour, 12, 12);
        hour.Promotion = 100;
        long t100 = DealerRules.VisitorThousandths(hour, 12, 12);
        hour.Promotion = 50;
        Check(t50 > 0 && t100 > t50 && DealerRules.VisitorThousandths(hour, 3, 12) < t50 && DealerRules.VisitorThousandths(hour, 12, 1) < t50,
            "dealer: visitors follow the game's promotion of the building, the hour of the day and how many cars the lot shows (" + t50 + " thousandths at noon)");
        var closed = hour;
        closed.Open = false;
        int carry = 700;
        Check(DealerRules.VisitorThousandths(closed, 12, 12) == 0 && DealerRules.VisitorThousandths(hour, 12, 0) == 0 && DealerRules.Visitors(closed, 12, 12, ref carry) == 0 && carry == 0,
            "dealer: a closed dealership or an empty lot has no visitors, and nothing is carried over a closed hour");
        var gloomy = hour;
        gloomy.Satisfaction = 0;
        Check(DealerRules.VisitorThousandths(gloomy, 12, 12) < t50, "dealer: the game's customer satisfaction of the building counts");
        int total = 0;
        carry = 0;
        for (int h = 0; h < 24; h++)
        {
            total += DealerRules.Visitors(hour, h, 12, ref carry);
        }
        Check(total >= 8 && total <= 30, "dealer: the reference dealership sees about ten to twenty visitors a day open all day (got " + total + ")");
        Check(DealerRules.Served(5, 0) == 0 && DealerRules.Served(5, 1) == 2 && DealerRules.Served(5, 3) == 5,
            "dealer: nobody on shift serves nobody, and each salesperson serves two an hour");
        Check(DealerRules.OfferChance(21000, 20000, 0, 50) > DealerRules.OfferChance(27000, 20000, 0, 50) && DealerRules.OfferChance(21000, 20000, 60, 50) < DealerRules.OfferChance(21000, 20000, 0, 50)
              && DealerRules.OfferChance(21000, 20000, 0, 100) > DealerRules.OfferChance(21000, 20000, 0, 0) && DealerRules.OfferChance(30000, 20000, 0, 100) < 30,
            "dealer: a dear car, a car that has stood long and a less skilled salesperson draw fewer offers; at half as much again as its worth almost none");

        var car = new StockCar { Id = 5, Asking = 30000, Bought = 0 };
        bool bounded = true, trades = true, drives = false, finance = false;
        for (int n = 0; n < 2000; n++)
        {
            var dice = new DealerDice(3, n);
            DealerOffer d = DealerRules.Offer(n, car, 100, 50, true, 5000, DealerModels, DealerColours, 1000, ref dice);
            bounded &= d.Offer >= DealerCatalogue.Share(30000, DealerCatalogue.OfferLeast[0]) - 50 && d.Ceiling >= d.Offer && d.Ceiling <= DealerCatalogue.Share(30000, DealerCatalogue.CeilingMost) + 50
                       && d.Expires == 100 + DealerCatalogue.DealHours;
            trades &= d.TradeType == null || (d.TradeAllowance > 0 && d.TradeAllowance <= DealerCatalogue.Share(d.Offer, DealerCatalogue.TradeInMost));
            drives |= d.WantsDrive;
            finance |= d.WantsFinance && d.Score >= DealerCatalogue.ScoreLeast && d.Term >= ProductCatalogue.CarTermLeast && d.Deposit >= DealerCatalogue.DepositLeast;
        }
        Check(bounded && trades && drives && finance,
            "dealer: a buyer's offer and ceiling are shares of the asking price within the catalogue's, a part exchange is worth at most its share, and some want a drive or finance");
        var deal = new DealerOffer { Id = 9, Offer = 27000, Ceiling = 30000 };
        Check(DealerRules.AcceptChance(deal, 26000) == 1000 && DealerRules.AcceptChance(deal, 30000) == DealerCatalogue.AcceptAtCeiling && DealerRules.AcceptChance(deal, 30050) == 0
              && DealerRules.AcceptChance(deal, 28500) > DealerRules.AcceptChance(deal, 29500),
            "dealer: a counter at the buyer's offer is taken, at their ceiling seldom, above it never");
        var twin = new DealerOffer { Id = 9, Offer = 27000, Ceiling = 30000 };
        bool same = DealerRules.Counter(deal, 29000, 5, out bool gone1) == DealerRules.Counter(twin, 29000, 5, out bool gone2) && gone1 == gone2 && deal.Offer == twin.Offer;
        Check(same, "dealer: the same counter at the same point of the haggle has the same answer");
        var stubborn = new DealerOffer { Id = 10, Offer = 27000, Ceiling = 27000 };
        bool g;
        DealerRules.Counter(stubborn, 29000, 5, out g);
        bool firstGone = g;
        DealerRules.Counter(stubborn, 29000, 5, out g);
        Check(!firstGone && g, "dealer: a buyer walks away after the second counter they will not take");
        Check(DealerRules.CounterAt(deal, 30000, 0) == deal.Offer && DealerRules.CounterAt(deal, 30000, DealerRules.CounterSteps(deal, 30000) - 1) == 30000,
            "dealer: the counters run from the buyer's offer to the asking price");
        var staffDeal = new DealerOffer { Offer = 28500, Stage = OfferStage.Open };
        Check(DealerRules.StaffClose(staffDeal, car, 950) && !DealerRules.StaffClose(staffDeal, car, 960) && !DealerRules.StaffClose(new DealerOffer { Offer = 29000, WantsDrive = true }, car, 950),
            "dealer: staff close at the dealership's floor, and not before a test drive the buyer asked for");
        var drive = new DealerOffer { Id = 3 };
        var driven = new StockCar { Id = 3, Km = 100, Condition = 900 };
        DealerRules.TestDrive(drive, driven, 50, 1);
        Check(drive.Drove && drive.Stage == OfferStage.Driving && driven.OutUntil == 51 && driven.Km >= 105 && driven.Km <= 130 && driven.Condition >= 880,
            "dealer: a test drive takes the car out for an hour and puts kilometres on it");

        var s = new DealerState { Seed = 4 };
        var y = new DealerYard { Place = "Main~1" };
        s.Yards.Add(y);
        for (int c = 0; c < 6; c++)
        {
            y.Cars.Add(new StockCar { Id = s.Next(), Type = DealerModels[c % 5].Type, Asking = 22000, Bought = 0, Spot = c });
        }
        var worths = new List<int> { 20000, 20000, 20000, 20000, 20000, 20000 };
        int offers = 0, visitors = 0;
        hour.Staff = 0;
        for (int h = 9; h < 19; h++)
        {
            DealerHour r = DealerRules.Hour(s, y, hour, 24 + h, 1, worths, 5000, DealerModels, DealerColours, 1000);
            offers += r.Offers;
            visitors += r.Visitors;
        }
        Check(visitors > 0 && offers == 0 && y.Today.Lost == visitors, "dealer: with nobody on shift every visitor is lost and no offer is made");
        hour.Staff = 3;
        for (int day = 2; day < 12; day++)
        {
            for (int h = 9; h < 19; h++)
            {
                offers += DealerRules.Hour(s, y, hour, day * 24 + h, day, worths, 5000, DealerModels, DealerColours, 1000).Offers;
            }
        }
        bool oneEach = true;
        for (int i = 0; i < y.Deals.Count; i++)
        {
            for (int j = 0; j < i; j++)
            {
                oneEach &= y.Deals[i].Car != y.Deals[j].Car;
            }
        }
        Check(offers > 0 && oneEach && y.Deals.Count <= y.Cars.Count, "dealer: with staff on shift offers come, one waiting for each car at most (" + offers + " in ten days)");
        DealerRules.CloseDay(y, 13);
        Check(y.Week[0].Day == 11 && y.Today.Day == 13, "dealer: a closed day goes into the week's books");
    }

    private static void DealerCredit()
    {
        Check(!DealerRules.Lends(570) && DealerRules.Lends(CreditCatalogue.LendingThreshold), "dealer: the dealership lends from the banks' own threshold");
        Check(DealerRules.RateBp(600) > DealerRules.RateBp(820), "dealer: a better rating borrows cheaper");
        DealMoney cash = DealerRules.Settle(20000, 4000, false, 0, 0, 0, 500, 60);
        Check(!cash.OnFinance && cash.Deposit == 16000 && cash.Premium == 500, "dealer: a cash sale is the price less the part exchange today");
        DealMoney fin = DealerRules.Settle(20000, 0, true, 20, 120, 700, 0, 60);
        Check(fin.OnFinance && fin.Deposit == 4000 && fin.Financed == 16000 && fin.Weeks == 18 && fin.Interest > 0 && (long)fin.Weekly * fin.Weeks >= fin.Financed + fin.Interest
              && (long)fin.Weekly * (fin.Weeks - 1) < fin.Financed + fin.Interest,
            "dealer: on finance the deposit is paid now, the rest is lent and paid back weekly with its interest (weekly " + fin.Weekly + ", interest " + fin.Interest + ")");
        DealMoney swap = DealerRules.Settle(20000, 6000, true, 20, 120, 700, 0, 60);
        Check(swap.Deposit == 0 && swap.Financed == 14000 && swap.Deposit + swap.Financed + swap.Allowance == 20000, "dealer: a part exchange worth more than the deposit lends less");

        var plan = new DealerPlan { Id = 1, Owed = 1000, Weekly = 300, Due = 7, Score = 800 };
        int paidIn = 0, weeks = 0;
        PlanWeek w = PlanWeek.Paid;
        while (w != PlanWeek.PaidOff && weeks < 40)
        {
            w = DealerRules.Week(plan, 1, 1000, out int paid);
            paidIn += paid;
            weeks++;
        }
        Check(w == PlanWeek.PaidOff && paidIn == 1000 && plan.Owed == 0, "dealer: a plan is paid off to the unit, the last instalment only what is left");
        var bad = new DealerPlan { Id = 2, Owed = 5000, Weekly = 300, Due = 7, Score = 590 };
        PlanWeek last = PlanWeek.Paid;
        for (int i = 0; i < 60 && last != PlanWeek.Defaulted; i++)
        {
            last = DealerRules.Week(bad, 1, 20000, out int paid);
        }
        Check(last == PlanWeek.Defaulted && bad.Missed == DealerCatalogue.MissesToDefault, "dealer: three weeks missed running and the car is taken back");
        Check(DealerRules.Recovery(10000) == 5500, "dealer: a car taken back fetches its share of its worth");

        int defaults = 0;
        for (int n = 0; n < 400; n++)
        {
            var p = new DealerPlan { Id = 100 + n, Owed = 3000, Weekly = 200, Due = 7, Score = 600 };
            for (int i = 0; i < 20; i++)
            {
                PlanWeek r = DealerRules.Week(p, 3, 1000, out int paid);
                if (r == PlanWeek.Defaulted)
                {
                    defaults++;
                    break;
                }
                if (r == PlanWeek.PaidOff)
                {
                    break;
                }
            }
        }
        Check(defaults < 40, "dealer: a fair rating seldom ends in a car taken back in an ordinary economy (" + defaults + " of 400)");

        Check(DealerRules.Premium(20000, 0, 150) == 700 && DealerRules.Premium(2000, 0, 150) == 150 && DealerRules.Premium(20000, -1, 150) == 0,
            "dealer: a warranty's premium is its share of the price, at least the least");
        var cover = new DealerCover { Id = 4, Tier = 2, Worth = 30000, Condition = 700, Until = 200 };
        int claims = 0, paidOut = 0;
        bool rests = true;
        int lastClaim = -100;
        for (int day = 80; day <= 260; day++)
        {
            int cost = DealerRules.Claim(cover, day, 9, 150, 5000);
            if (cost > 0)
            {
                claims++;
                paidOut += cost;
                rests &= day - lastClaim > DealerCatalogue.ClaimRest && day <= 200 && cost >= 150 && cost <= 5000;
                lastClaim = day;
            }
        }
        Check(claims <= DealerCatalogue.ClaimsMost && rests && cover.Paid == paidOut, "dealer: claims rest between them, stop at the warranty's last day and at the most it pays (" + claims + ")");
    }

    private static void DealerBooks()
    {
        Check(DealerLines.Of(DealerLines.SoldType) == DealerLine.Takings && DealerLines.Of(DealerLines.FinanceKey) == DealerLine.Takings
              && DealerLines.Of(DealerLines.PremiumKey) == DealerLine.Takings && DealerLines.Of(DealerLines.RecoveryKey) == DealerLine.Takings,
            "dealer: a car sold, an instalment, a premium and a car taken back and sold are takings");
        Check(DealerLines.Of(DealerLines.BoughtType) == DealerLine.Stock && DealerLines.Of(DealerLines.InspectionKey) == DealerLine.Stock
              && DealerLines.Of(DealerLines.FeeKey) == DealerLine.Stock && DealerLines.Of(DealerLines.PrepareKey) == DealerLine.Stock && DealerLines.Of(DealerLines.ClaimKey) == DealerLine.Running,
            "dealer: cars bought, inspections, the auction's fee and preparing are the stock's cost, claims the running cost");
        Check(DealerLines.Of("ba:transaction_wage") == DealerLine.None && DealerLines.Of("ba:transaction_rent") == DealerLine.None && DealerLines.Of(null) == DealerLine.None
              && DealerLines.RowOf(DealerLine.Stock) == DealerLines.StockRow && DealerLines.RowOf(DealerLine.Takings) == null && DealerLines.StockRow.StartsWith("reallife_"),
            "dealer: the game's own wages and rent at a dealership are not guessed, and each cost has a row of the mod's");
    }

    private static void DealerSave()
    {
        var s = new DealerState { Seed = 77, FirstDay = 3, LastHour = 400, LastDay = 16, NextId = 90, MarketDay = 16, NoBidsUntil = 18 };
        var y = new DealerYard { Place = "Main%20St~12", Name = "Mark's | Motors; fine", StaffFloor = 930, Finance = false, Warranty = true, VisitorCarry = 456 };
        y.Spots.Add(new LotSpot { X = -120, Y = 5, Z = 300, Yaw = 90 });
        y.Spots.Add(new LotSpot { X = 220, Y = 5, Z = 300, Yaw = 90 });
        y.Cars.Add(new StockCar { Id = 4, VehicleId = "Ab+c/9==", Type = "ba:vehicletype_bima320", Colour = "Red", Km = 41000, Condition = 760, Faults = 5, Known = 1, Cost = 18000, Asking = 23450, Bought = 12, Source = CarSource.Used, Spot = 1, OutUntil = 401 });
        y.Deals.Add(new DealerOffer { Id = 7, Car = 4, Customer = "Rosa Lambert", Profile = 2, Offer = 22000, Ceiling = 23900, Counters = 1, Made = 390, Expires = 414, Stage = OfferStage.Driving, Drove = true, WantsDrive = true,
            WantsFinance = true, Score = 700, Deposit = 20, Term = 120, Tier = 1, TradeType = "ba:vehicletype_honzamimic", TradeColour = "Black", TradeKm = 90000, TradeCondition = 600, TradeAllowance = 3100 });
        y.Today = new DealerBook { Day = 16, Visitors = 9, Served = 7, Lost = 2, Offers = 3, Sold = 1, Takings = 23000, Bought = 18000, Claims = 300, Margin = 4000 };
        y.Week[0] = new DealerBook { Day = 15, Visitors = 4 };
        s.Yards.Add(y);
        s.Market.Add(new DealerListing { Id = 50, Kind = ListingKind.Auction, Type = "ba:vehicletype_vordpony", Colour = "Silver", Km = 60000, Condition = 700, Shown = 740, Faults = 2, Inspected = true, Asking = 21000, Floor = 0,
            Haggles = 0, Countered = 0, Ends = 405, Leader = 3, Ceiling = 30000, Bids = 6, Gone = false });
        s.Plans.Add(new DealerPlan { Id = 60, Place = "Main%20St~12", Customer = "Ben Osei", Type = "ba:vehicletype_bima320", Worth = 20000, Owed = 9000, Weekly = 640, Due = 21, Missed = 1, Paid = 3200, Score = 650 });
        s.Covers.Add(new DealerCover { Id = 61, Place = "Main%20St~12", Customer = "Ben Osei", Type = "ba:vehicletype_bima320", Tier = 2, Worth = 20000, Condition = 800, Until = 130, Claims = 1, Paid = 700, RestUntil = 25 });
        s.Sales.Add(new DealerSale { Day = 14, Place = "Main%20St~12", Customer = "Ben Osei", Type = "ba:vehicletype_bima320", Price = 24000, Cost = 19000, Financed = true, Tier = 2 });
        string line = DealerStore.Write(s);
        DealerState back = DealerStore.Read(line, out int version);
        Check(back != null && version == DealerStore.Version && DealerStore.Write(back) == line, "dealer: the save's line reads back to exactly what was written");
        Check(back != null && back.Yards.Count == 1 && back.Yards[0].Name == "Mark's   Motors  fine" && back.Yards[0].Cars[0].VehicleId == "Ab+c/9==" && back.Yards[0].Deals[0].TradeAllowance == 3100
              && back.Yards[0].Spots.Count == 2 && back.Market[0].Leader == 3 && back.Plans[0].Missed == 1 && back.Covers[0].RestUntil == 25 && back.Sales[0].Tier == 2 && !back.Yards[0].Finance,
            "dealer: every part comes back, a name with the line's own marks made plain");
        DealerState later = DealerStore.Read(line.Replace("^m:", "^z:something new|1;2^m:").Replace("|456^p:", "|456|99|extra^p:"), out version);
        Check(later != null && DealerStore.Write(later) == line, "dealer: a line of a later version, with a new part and new fields, still loads");
        Check(DealerStore.Read("nonsense", out version) == null && DealerStore.Read("v=1^y:a|b", out version) == null && DealerStore.Read(null, out version) == null,
            "dealer: a line that is not the dealership's is not taken for one");
    }

    // A month of the reference dealership: a mid-sized unit at a fair promotion, two salespeople of middling skill from 9 to
    // 19 every day; a player who keeps the lot full from the wholesaler, lets the staff close at 95% and counters the rest
    // once at the asking price. What it makes before wages and rent is measured against the bound the catalogue states.
    private static void DealerBalance()
    {
        var s = new DealerState { Seed = 2026 };
        var y = new DealerYard { Place = "Ref~1" };
        s.Yards.Add(y);
        var hour = new YardHour { Open = true, ShopCustomers = 25f, Promotion = 50, BaseMultiplier = 0.5f, Satisfaction = 50, Staff = 2, Skill = 50 };
        var worths = new List<int>();
        long margin = 0;
        int sold = 0, days = 30;
        for (int day = 1; day <= days; day++)
        {
            DealerRules.Morning(s, day, DealerModels, DealerColours, 1000);
            for (int i = 0; i < s.Market.Count && y.Cars.Count < DealerCatalogue.CarsMost; i++)
            {
                DealerListing l = s.Market[i];
                if (l.Kind != ListingKind.Wholesale)
                {
                    continue;
                }
                int worth = DealerRules.Worth(Model(l.Type).Price, 1000, l.Km, 1000);
                y.Cars.Add(new StockCar { Id = s.Next(), Type = l.Type, Km = l.Km, Condition = 1000, Cost = l.Asking, Asking = DealerRules.Asking(worth), Bought = day, Spot = y.Cars.Count });
            }
            for (int h = 9; h < 19; h++)
            {
                worths.Clear();
                for (int c = 0; c < y.Cars.Count; c++)
                {
                    worths.Add(DealerRules.Worth(Model(y.Cars[c].Type).Price, 1000, y.Cars[c].Km, y.Cars[c].Condition));
                }
                DealerRules.Hour(s, y, hour, day * 24 + h, day, worths, 5000, DealerModels, DealerColours, 1000);
                for (int d = y.Deals.Count - 1; d >= 0; d--)
                {
                    DealerOffer deal = y.Deals[d];
                    StockCar car = y.Car(deal.Car);
                    bool closes = DealerRules.StaffClose(deal, car, y.StaffFloor);
                    if (!closes && deal.WantsDrive && !deal.Drove)
                    {
                        DealerRules.TestDrive(deal, car, day * 24 + h, s.Seed);
                        continue;
                    }
                    if (!closes && deal.Stage == OfferStage.Open)
                    {
                        closes = DealerRules.Counter(deal, car.Asking, s.Seed, out bool gone);
                        if (!closes && gone)
                        {
                            y.Deals.RemoveAt(d);
                            continue;
                        }
                    }
                    if (closes)
                    {
                        margin += deal.Offer - car.Cost;
                        sold++;
                        y.Deals.RemoveAt(d);
                        y.Cars.Remove(car);
                    }
                }
            }
        }
        long perDay = margin / days;
        Console.WriteLine("  dealer balance: the reference dealership made " + perDay + " a day on " + sold + " cars in " + days + " days before wages and rent.");
        Check(perDay >= 3000 && perDay <= 7000 && sold >= days / 2,
            "dealer balance: the reference dealership makes " + perDay + " a day on " + sold + " cars in " + days + " days before wages and rent, inside the catalogue's 3,000 to 7,000");
    }
}
