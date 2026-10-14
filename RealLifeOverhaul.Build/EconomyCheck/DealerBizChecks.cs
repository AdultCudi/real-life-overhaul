using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy.Dealership;

// The Car Showroom's business (Scripts/Economy/Dealer/DealerRules.Business.cs, DealerReport.cs): advertising events and
// how they meet the building's promotion, the showroom's reputation, its steps and perks, missions, extras sold with a
// car, the workshop, cars won at auction on their way, the reports' sums, the save's second version and a first
// version's line, every text the rules name, and how long an hour and a report take.
internal static partial class Program
{
    private static readonly MarketModel[] BizModels =
    {
        new MarketModel("ba:vehicletype_honzamimic", 9000f, 1), new MarketModel("ba:vehicletype_bima320", 32000f, 2), new MarketModel("ba:vehicletype_mersaidis500", 85000f, 3),
        new MarketModel("ba:vehicletype_vordpony", 45000f, 4), new MarketModel("ba:vehicletype_umcnunavut", 52000f, 5)
    };

    private static void DealerBusiness()
    {
        DealerEvents();
        DealerReputation();
        DealerMissions();
        DealerExtras();
        DealerWorkshop();
        DealerDeliveries();
        DealerReports();
        DealerSaveTwo();
        DealerBizTexts();
        DealerBizTiming();
    }

    private static DealerYard BizYard(string place, int cars, int condition)
    {
        var y = new DealerYard { Place = place, Name = place };
        for (int s = 0; s < 6; s++)
        {
            y.Spots.Add(new LotSpot { X = s * 340 });
        }
        for (int c = 0; c < cars; c++)
        {
            y.Cars.Add(new StockCar { Id = 100 + c, VehicleId = "v" + c, Type = BizModels[c % BizModels.Length].Type, Asking = 21000, Bought = 0, Spot = c, Condition = condition });
        }
        return y;
    }

    private static void DealerEvents()
    {
        var y = BizYard("Ev~1", 3, 1000);
        Check(DealerRules.CanBook(y, DealerCatalogue.OpenWeekend, 10) == EventRefusal.None && DealerRules.CanBook(y, 9, 10) == EventRefusal.Kind && DealerRules.CanBook(y, -1, 10) == EventRefusal.Kind,
            "dealer events: a showroom with none running may book any of the kinds, and nothing else");
        DealerRules.Book(y, DealerCatalogue.OpenWeekend, 0, 10);
        int last = 10 + DealerCatalogue.EventDays[DealerCatalogue.OpenWeekend] - 1;
        Check(y.EventOn(10) && y.EventOn(last) && !y.EventOn(last + 1) && !y.EventOn(9) && y.Tally[DealerCatalogue.TallyEvents] == 1,
            "dealer events: an event runs from the day it is booked for its days, and counts for the missions");
        Check(DealerRules.CanBook(y, DealerCatalogue.TestDriveDays, 11) == EventRefusal.Running, "dealer events: one event at a time at a showroom");
        Check(!DealerRules.EventOver(y, last) && DealerRules.EventOver(y, last + 1), "dealer events: an event is over the day after its last");
        DealerRules.EndEvent(y);
        Check(y.Event == -1 && y.EventLast[DealerCatalogue.OpenWeekend] == last && DealerRules.CanBook(y, DealerCatalogue.OpenWeekend, last + 1) == EventRefusal.Rested
              && DealerRules.CanBook(y, DealerCatalogue.OpenWeekend, last + DealerCatalogue.EventRest) == EventRefusal.Rested
              && DealerRules.CanBook(y, DealerCatalogue.OpenWeekend, last + DealerCatalogue.EventRest + 1) == EventRefusal.None
              && DealerRules.CanBook(y, DealerCatalogue.TestDriveDays, last + 1) == EventRefusal.None,
            "dealer events: the same kind rests " + DealerCatalogue.EventRest + " days after it ends at that showroom; another kind may follow at once");

        // Visitors through the promotion, never past the game's top: no double counting with a campaign that has it high.
        Check(DealerRules.Lifted(50, 15) == 65 && DealerRules.Lifted(95, 15) == 100 && DealerRules.Lifted(100, 15) == 100 && DealerRules.Lifted(40, -5) == 40,
            "dealer events: an event's points go on the building's promotion and never take it past " + DealerCatalogue.PromotionMost);
        var plain = new DealerYard { Place = "P~1" };
        plain.Extras = false;
        OfferMood none = DealerRules.Mood(plain, 5);
        Check(none.Promotion == 0 && none.Convert == 1000 && none.First == 0 && none.Accept == 0 && none.Drive == 0 && none.TradeIn == 0 && none.Allowance == 0 && none.TakeUp == 0 && !none.Extras && none.Featured == 0,
            "dealer events: with no event, no perk and no extras the buyers are as the rules made them before");
        var open = BizYard("Op~1", 3, 1000);
        DealerRules.Book(open, DealerCatalogue.OpenWeekend, 0, 5);
        OfferMood m = DealerRules.Mood(open, 5);
        Check(m.Promotion == DealerCatalogue.EventPromotion[0] && m.Convert == DealerCatalogue.EventOffer[0] && DealerRules.Mood(open, 5 + DealerCatalogue.EventDays[0]).Promotion == 0,
            "dealer events: an open weekend's points and offer chance hold on its days only");
        var hour = new YardHour { Open = true, ShopCustomers = 25f, Promotion = 50, BaseMultiplier = 0.5f, Satisfaction = 50, Staff = 2, Skill = 50 };
        YardHour lifted = hour;
        lifted.Promotion = DealerRules.Lifted(hour.Promotion, m.Promotion);
        YardHour top = hour, topLifted = hour;
        top.Promotion = 100;
        topLifted.Promotion = DealerRules.Lifted(100, m.Promotion);
        Check(DealerRules.VisitorThousandths(lifted, 12, 3) > DealerRules.VisitorThousandths(hour, 12, 3) && DealerRules.VisitorThousandths(topLifted, 12, 3) == DealerRules.VisitorThousandths(top, 12, 3),
            "dealer events: an open weekend brings more visitors, and none more where a campaign has the promotion at its top already");

        // A whole event's days against the same days without it, the same seed: more visitors and more offers.
        int withV = 0, withO = 0, withoutV = 0, withoutO = 0;
        for (int run = 0; run < 80; run++)
        {
            var s = new DealerState { Seed = 33 + run / 2 };
            var yy = BizYard("Run~1", 6, 1000);
            s.Yards.Add(yy);
            if (run % 2 == 0)
            {
                DealerRules.Book(yy, DealerCatalogue.OpenWeekend, 0, 1);
            }
            var worths = new List<int> { 20000, 20000, 20000, 20000, 20000, 20000 };
            for (int day = 1; day <= 3; day++)
            {
                for (int h = 9; h < 19; h++)
                {
                    DealerHour r = DealerRules.Hour(s, yy, hour, day * 24 + h, day, worths, 5000, BizModels, DealerColours, 1000);
                    if (run % 2 == 0) { withV += r.Visitors; withO += r.Offers; } else { withoutV += r.Visitors; withoutO += r.Offers; }
                    yy.Deals.Clear();
                }
            }
        }
        Check(withV > withoutV && withO > withoutO, "dealer events: three days of an open weekend, in forty showrooms, bring more visitors and offers than the same days without it (" + withV + " and " + withO + " against " + withoutV + " and " + withoutO + ")");

        // A featured kind: its cars are the ones of that kind, and they are looked at far more often.
        var f = BizYard("Fe~1", 5, 1000);
        DealerRules.Book(f, DealerCatalogue.FeaturedKind, 3, 1);
        int mask = DealerRules.FeaturedCars(f, BizModels, DealerRules.Mood(f, 1).Featured);
        Check(mask == 1 << 2 && DealerRules.FeaturedCars(f, BizModels, 0) == 0 && f.Feature == 3, "dealer events: a featured event marks the cars of its kind of car, and only those");
        var fw = new List<int> { 20000, 20000, 20000, 20000, 20000 };
        int picks = 0, featuredPicks = 0;
        for (int i = 0; i < 4000; i++)
        {
            var dice = new DealerDice(7, i, 1);
            int c = DealerRules.LookAt(f, 30, 1, fw, mask, ref dice);
            picks += c >= 0 ? 1 : 0;
            featuredPicks += c == 2 ? 1 : 0;
        }
        Check(picks == 4000 && featuredPicks > 4000 / 5 * 12 / 10, "dealer events: the featured car is looked at more often than its share (" + featuredPicks + " of 4000 against 800)");

        // The offer of a buyer who came during test drive days and a part exchange week.
        var car = new StockCar { Id = 1, Type = BizModels[1].Type, Asking = 30000, Condition = 1000 };
        int drivesPlain = 0, drivesEvent = 0, tradesPlain = 0, tradesEvent = 0;
        var drive = OfferMood.Plain;
        drive.Drive = DealerCatalogue.EventDrive[1];
        drive.Accept = DealerCatalogue.EventAccept[1];
        var trade = OfferMood.Plain;
        trade.TradeIn = DealerCatalogue.EventTradeIn[2];
        trade.Allowance = DealerCatalogue.EventAllowance[2];
        bool same = true, lift = true;
        for (int i = 0; i < 3000; i++)
        {
            var d1 = new DealerDice(5, i, 3);
            var d2 = new DealerDice(5, i, 3);
            var d3 = new DealerDice(5, i, 3);
            var d4 = new DealerDice(5, i, 3);
            DealerOffer a = DealerRules.Offer(i, car, 10, 50, true, 5000, BizModels, DealerColours, 1000, ref d1);
            DealerOffer b = DealerRules.Offer(i, car, 10, 50, true, 5000, BizModels, DealerColours, 1000, OfferMood.Plain, ref d2);
            DealerOffer c = DealerRules.Offer(i, car, 10, 50, true, 5000, BizModels, DealerColours, 1000, drive, ref d3);
            DealerOffer e = DealerRules.Offer(i, car, 10, 50, true, 5000, BizModels, DealerColours, 1000, trade, ref d4);
            same &= a.Offer == b.Offer && a.Ceiling == b.Ceiling && a.Tier == b.Tier && a.TradeType == b.TradeType && a.Extras == 0 && b.Extras == 0 && a.Lift == 0;
            lift &= c.Lift == DealerCatalogue.EventAccept[1];
            drivesPlain += a.WantsDrive ? 1 : 0;
            drivesEvent += c.WantsDrive ? 1 : 0;
            tradesPlain += a.TradeType != null ? 1 : 0;
            tradesEvent += e.TradeType != null ? 1 : 0;
        }
        Check(same, "dealer events: a plain mood draws exactly the offer the rules drew before (the same seed, the same buyer)");
        Check(lift && drivesEvent > drivesPlain * 13 / 10, "dealer events: on test drive days more buyers ask for a drive (" + drivesEvent + " against " + drivesPlain + ") and their counter's lift is kept on the offer");
        Check(tradesEvent > tradesPlain * 13 / 10, "dealer events: in a part exchange week more buyers bring a car (" + tradesEvent + " against " + tradesPlain + ")");
        var dd = new DealerOffer { Offer = 26000, Ceiling = 30000 };
        int before = DealerRules.AcceptChance(dd, 29000);
        dd.Lift = DealerCatalogue.EventAccept[1];
        Check(DealerRules.AcceptChance(dd, 29000) == before + DealerCatalogue.EventAccept[1] && DealerRules.AcceptChance(dd, 30050) == 0 && DealerRules.AcceptChance(dd, 26000) == 1000,
            "dealer events: an offer's lift makes a counter more likely to be taken, never one past the buyer's ceiling");
        var a2 = new DealerDice(9, 1, 2);
        var withAllowance = trade;
        withAllowance.TradeIn = 1000;
        var noAllowance = withAllowance;
        noAllowance.Allowance = 0;
        DealerOffer hi = DealerRules.Offer(1, car, 10, 50, true, 5000, BizModels, DealerColours, 1000, withAllowance, ref a2);
        var a3 = new DealerDice(9, 1, 2);
        DealerOffer lo = DealerRules.Offer(1, car, 10, 50, true, 5000, BizModels, DealerColours, 1000, noAllowance, ref a3);
        Check(hi.TradeType == null || lo.TradeType == null || hi.TradeAllowance >= lo.TradeAllowance, "dealer events: a part exchange week allows more for the same buyer's car (" + hi.TradeAllowance + " against " + lo.TradeAllowance + ")");
    }

    private static void DealerReputation()
    {
        Check(DealerRules.Step(0) == 0 && DealerRules.Step(99) == 0 && DealerRules.Step(100) == 1 && DealerRules.Step(250) == 2 && DealerRules.Step(449) == 2 && DealerRules.Step(450) == 3
              && DealerRules.Step(700) == 4 && DealerRules.Step(1000) == 4, "dealer reputation: the steps start where the catalogue says");
        bool rising = true;
        for (int k = 1; k < DealerCatalogue.RepSteps.Length; k++)
        {
            rising &= DealerCatalogue.RepSteps[k] > DealerCatalogue.RepSteps[k - 1] && DealerCatalogue.RepSteps[k] <= DealerCatalogue.RepMost;
        }
        Check(rising && DealerCatalogue.RepSteps[0] == 0, "dealer reputation: each step starts higher than the last, inside the most");
        var y = new DealerYard { Place = "R~1" };
        int was = DealerRules.Repute(y, 120);
        Check(was == 0 && y.Rep == 120 && DealerRules.Repute(y, -500) == 1 && y.Rep == 0 && DealerRules.Repute(y, 5000) == 0 && y.Rep == DealerCatalogue.RepMost,
            "dealer reputation: a change is kept from 0 to the most, and the step before it is answered");
        Check(DealerRules.SaleRep(1000, 0) == DealerCatalogue.RepSale + DealerCatalogue.RepGoodCar && DealerRules.SaleRep(700, 0) == DealerCatalogue.RepSale
              && DealerRules.SaleRep(700, 1) == DealerCatalogue.RepSale - DealerCatalogue.RepFault && DealerRules.SaleRep(700, 63) == DealerCatalogue.RepSale - DealerCatalogue.RepFaultsMost,
            "dealer reputation: a sale adds, a fine car more, each fault the buyer finds takes some, and never more than the most");
        var p = new DealerYard { Place = "Pk~1", Extras = false };
        p.Rep = DealerCatalogue.RepSteps[1];
        OfferMood m1 = DealerRules.Mood(p, 1);
        p.Rep = DealerCatalogue.RepSteps[2];
        OfferMood m2 = DealerRules.Mood(p, 1);
        p.Rep = DealerCatalogue.RepSteps[4];
        OfferMood m4 = DealerRules.Mood(p, 1);
        Check(m1.First == DealerCatalogue.PerkOffer && m1.TakeUp == 0 && m1.Convert == 1000 && m2.TakeUp == DealerCatalogue.PerkExtras && m4.Convert == DealerCatalogue.PerkConvert && m4.First == DealerCatalogue.PerkOffer,
            "dealer reputation: each step's perk holds from that step on");
        Check(DealerRules.WholesalePrice(20000, DealerCatalogue.RepSteps[2]) == 20000 && DealerRules.WholesalePrice(20000, DealerCatalogue.RepSteps[3]) == 19800,
            "dealer reputation: a respected showroom buys new cars " + DealerCatalogue.PerkWholesale / 10 + "% under the wholesaler's price");
        var open = new DealerYard { Place = "Pk~2" };
        open.Rep = DealerCatalogue.RepSteps[4];
        DealerRules.Book(open, DealerCatalogue.OpenWeekend, 0, 1);
        Check(DealerRules.Mood(open, 1).Convert == DealerCatalogue.PerkConvert * DealerCatalogue.EventOffer[0] / 1000, "dealer reputation: a renowned showroom's perk and an event's offer chance multiply");

        // An offer left to lapse costs a point; a lot that is looked after keeps its name.
        var s = new DealerState { Seed = 3 };
        var lapse = BizYard("La~1", 2, 1000);
        lapse.Rep = 50;
        s.Yards.Add(lapse);
        lapse.Deals.Add(new DealerOffer { Id = 1, Car = 100, Expires = 30 });
        lapse.Deals.Add(new DealerOffer { Id = 2, Car = 999, Expires = 30 });
        var closed = new YardHour();
        DealerRules.Hour(s, lapse, closed, 30, 1, new List<int> { 20000, 20000 }, 5000, BizModels, DealerColours, 1000);
        Check(lapse.Deals.Count == 0 && lapse.Rep == 50 - DealerCatalogue.RepLapsed, "dealer reputation: an offer left to lapse costs a point; one whose car has gone does not");
    }

    private static void DealerMissions()
    {
        int sum = 0;
        bool tallies = true, targets = true;
        for (int m = 0; m < DealerCatalogue.Missions; m++)
        {
            sum += DealerCatalogue.MissionReward[m];
            tallies &= DealerCatalogue.MissionTally[m] >= 0 && DealerCatalogue.MissionTally[m] < DealerCatalogue.Tallies;
            targets &= DealerCatalogue.MissionTarget[m] > 0 && DealerCatalogue.MissionReward[m] > 0;
        }
        Check(DealerCatalogue.MissionTarget.Length == DealerCatalogue.Missions && DealerCatalogue.MissionReward.Length == DealerCatalogue.Missions && DealerCatalogue.Missions <= 31 && tallies && targets,
            "dealer missions: every mission counts a tally there is, with a target and a reward, and all fit one number's bits");
        Check(sum < DealerCatalogue.RepMost && sum >= DealerCatalogue.RepSteps[2], "dealer missions: together they give " + sum + " reputation: past the second step, never the whole of it on their own");
        var y = new DealerYard { Place = "M~1" };
        var done = new List<int>();
        Check(DealerRules.Missions(y, done) == 0 && done.Count == 0 && y.Done == 0, "dealer missions: nothing done, nothing given");
        DealerRules.Count(y, DealerCatalogue.TallySold, 1);
        int given = DealerRules.Missions(y, done);
        Check(given == DealerCatalogue.MissionReward[0] && done.Count == 1 && done[0] == 0 && DealerRules.MissionDone(y, 0) && y.Rep == given,
            "dealer missions: the first sale is done at once and gives its reputation");
        Check(DealerRules.Missions(y, done) == 0 && done.Count == 0, "dealer missions: a mission is given once");
        DealerRules.Count(y, DealerCatalogue.TallySold, 49);
        DealerRules.Count(y, DealerCatalogue.TallyEvents, 1);
        given = DealerRules.Missions(y, done);
        Check(given == DealerCatalogue.MissionReward[2] + DealerCatalogue.MissionReward[9] + DealerCatalogue.MissionReward[11] && done.Count == 3 && done[0] == 2 && done[1] == 9 && done[2] == 11,
            "dealer missions: every mission reached is done in its order, a tally reaching several targets at once (" + given + ")");
        DealerRules.Most(y, DealerCatalogue.TallyLot, 4);
        DealerRules.Most(y, DealerCatalogue.TallyLot, 2);
        DealerRules.Count(y, DealerCatalogue.TallyQuick, 0);
        DealerRules.Count(y, DealerCatalogue.TallyQuick, -3);
        Check(y.Tally[DealerCatalogue.TallyLot] == 4 && y.Tally[DealerCatalogue.TallyQuick] == 0 && DealerRules.Progress(y, 1) == 4 && DealerRules.Progress(y, 11) == 50,
            "dealer missions: the cars on the lot keep the most reached, a count never goes back, and progress stops at the target");
        var full = new DealerYard { Place = "M~2" };
        for (int t = 0; t < DealerCatalogue.Tallies; t++)
        {
            full.Tally[t] = 1000;
        }
        Check(DealerRules.Missions(full, done) == sum && done.Count == DealerCatalogue.Missions && full.Done == (1 << DealerCatalogue.Missions) - 1, "dealer missions: every mission done gives the whole of their rewards");
    }

    private static void DealerExtras()
    {
        Check(DealerRules.ExtraPrice(30000, 0, 150) == 450 && DealerRules.ExtraPrice(2000, 0, 150) == 150 && DealerRules.ExtraPrice(30000, 9, 150) == 0 && DealerRules.ExtraPrice(0, 1, 150) == 0,
            "dealer extras: an extra is its share of the car's price, at least its least");
        bool margins = true;
        for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
        {
            int sell = DealerRules.ExtraPrice(40000, e, DealerCatalogue.ExtraLeast[e]);
            int buy = DealerRules.ExtraCost(sell, e);
            margins &= sell > 0 && buy > 0 && buy < sell && DealerCatalogue.ExtraTakeUp[e] > 0 && DealerCatalogue.ExtraTakeUp[e] < 1000;
        }
        Check(margins && DealerLines.ExtraKeys.Length == DealerCatalogue.ExtraKinds, "dealer extras: each leaves the showroom a margin, is taken by some buyers and not all, and has its own line");
        var off = OfferMood.Plain;
        var on = OfferMood.Plain;
        on.Extras = true;
        var perk = on;
        perk.TakeUp = DealerCatalogue.PerkExtras;
        int[] taken = new int[DealerCatalogue.ExtraKinds];
        int anyOff = 0, perkTaken = 0, keenTaken = 0, plainTaken = 0;
        const int n = 20000;
        for (int i = 0; i < n; i++)
        {
            var d1 = new DealerDice(4, i, 7);
            var d2 = new DealerDice(4, i, 7);
            var d3 = new DealerDice(4, i, 7);
            var d4 = new DealerDice(4, i, 7);
            anyOff += DealerRules.DrawExtras(1, off, ref d1);
            int x = DealerRules.DrawExtras(1, on, ref d2);
            for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
            {
                taken[e] += (x & (1 << e)) != 0 ? 1 : 0;
            }
            plainTaken += DealerRules.ExtraCount(x);
            perkTaken += DealerRules.ExtraCount(DealerRules.DrawExtras(1, perk, ref d3));
            keenTaken += DealerRules.ExtraCount(DealerRules.DrawExtras(2, on, ref d4));
        }
        bool rates = true;
        for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
        {
            int want = DealerCatalogue.ExtraTakeUp[e] * n / 1000;
            rates &= Math.Abs(taken[e] - want) < want / 10 + 50;
        }
        Check(anyOff == 0 && rates, "dealer extras: a showroom that sells none sells none, and each is taken at its take-up (" + string.Join(", ", taken) + " of " + n + ")");
        Check(perkTaken > plainTaken && keenTaken > plainTaken, "dealer extras: a trusted showroom's perk and a keen buyer take more (" + perkTaken + ", " + keenTaken + " against " + plainTaken + ")");
        Check(DealerLines.Of(DealerLines.ExtraKeys[0]) == DealerLine.Takings && DealerLines.Of(DealerLines.ExtraKeys[3]) == DealerLine.Takings && DealerLines.Of(DealerLines.ExtrasCostKey) == DealerLine.Stock
              && DealerLines.Of(DealerLines.WorkshopKey) == DealerLine.Stock && DealerLines.Of(DealerLines.EventKey) == DealerLine.Running && DealerLines.Of("reallife_transaction_dealer_extra_nothing") == DealerLine.None,
            "dealer extras: an extra sold is takings and bought in the stock's cost; the workshop is the stock's cost and an event the running cost");
    }

    private static void DealerWorkshop()
    {
        bool falls = true, full = true;
        for (int c = 300; c < 1000; c += 10)
        {
            falls &= DealerRules.ConditionDraw(c) <= DealerRules.ConditionDraw(c + 10);
        }
        for (int c = 950; c <= 1000; c += 5)
        {
            full &= DealerRules.ConditionDraw(c) == 1000;
        }
        Check(falls && full && DealerRules.ConditionDraw(500) < 1000, "dealer workshop: a car's condition draws buyers, a tired one fewer, one from 950 up in full");
        Check(DealerRules.OfferChance(21000, 20000, 0, 50, 600, 1000) < DealerRules.OfferChance(21000, 20000, 0, 50, 1000, 1000)
              && DealerRules.OfferChance(21000, 20000, 0, 50) == DealerRules.OfferChance(21000, 20000, 0, 50, 1000, 1000),
            "dealer workshop: a tired car draws fewer offers; a fine one as many as before");
        int faults = (1 << 0) | (1 << 4);
        int worked = DealerRules.Worked(600, faults), prepared = DealerRules.Prepared(600, faults, faults);
        Check(worked > prepared && worked <= 1000 && DealerRules.Worked(1000, 0) == 1000 && DealerRules.Worked(990, 0) == 1000,
            "dealer workshop: the workshop brings a car further back than preparing it (" + worked + " against " + prepared + ")");
        Check(DealerRules.Worked(600, faults) > DealerRules.Worked(600, 0) - 1 && DealerRules.Worked(600, 0) >= 600 + (400 * DealerCatalogue.WorkshopRestore / 1000),
            "dealer workshop: every fault is put right, then most of what is missing");
        int cost = DealerRules.WorkshopCost(30000, 600, faults, 250), costClean = DealerRules.WorkshopCost(30000, 600 + DealerRules.FaultCondition(faults), 0, 250);
        Check(cost > costClean && costClean > 250 && DealerRules.WorkshopCost(30000, 1000, 0, 250) == 250, "dealer workshop: it charges for the bay, each fault and the condition missing (" + cost + ", " + costClean + ")");
        int gain = DealerRules.Worth(30000f, 1000, 40000, worked) - DealerRules.Worth(30000f, 1000, 40000, 600);
        Check(gain > 0 && gain + 0 > cost / 2, "dealer workshop: the worth a car gains is of the order of what the work costs (" + gain + " for " + cost + ")");
        Check(DealerRules.WorkshopHours(1000) == DealerCatalogue.WorkshopHoursLeast && DealerRules.WorkshopHours(0) == DealerCatalogue.WorkshopHoursMost
              && DealerRules.WorkshopHours(800) == DealerCatalogue.WorkshopHoursLeast + 200 / DealerCatalogue.WorkshopPerHour && DealerRules.WorkshopHours(700) > DealerRules.WorkshopHours(900),
            "dealer workshop: the work takes a day, longer for a car missing more, within the most");
        Check(!DealerRules.WorkshopWorth(new StockCar { Condition = 1000 }) && DealerRules.WorkshopWorth(new StockCar { Condition = 999 }) && DealerRules.WorkshopWorth(new StockCar { Condition = 1000, Faults = 2 }),
            "dealer workshop: a perfect car has nothing for the workshop to do");
        var y = BizYard("W~1", 3, 1000);
        y.Cars[0].ShopUntil = 50;
        y.Cars[1].ShopUntil = 50;
        var worths = new List<int> { 20000, 20000, 20000 };
        bool never = true;
        for (int i = 0; i < 500; i++)
        {
            var dice = new DealerDice(1, i);
            never &= DealerRules.LookAt(y, 40, 1, worths, 0, ref dice) == 2;
        }
        Check(never && y.InWorkshop(40) == 2 && y.InWorkshop(50) == 0, "dealer workshop: a car in the workshop is not on show until its hour");
    }

    private static void DealerDeliveries()
    {
        bool inside = true, same = true;
        for (int lot = 0; lot < 200; lot++)
        {
            int now = 24 * 10 + lot % 24;
            int at = DealerRules.DeliveryDue(9, lot, now);
            int days = at / 24 - now / 24;
            inside &= days >= DealerCatalogue.DeliveryDaysLeast && days <= DealerCatalogue.DeliveryDaysMost && at % 24 == DealerCatalogue.DeliveryHour;
            same &= at == DealerRules.DeliveryDue(9, lot, now);
        }
        Check(inside && same, "dealer deliveries: a lot won arrives " + DealerCatalogue.DeliveryDaysLeast + " to " + DealerCatalogue.DeliveryDaysMost + " days later at " + DealerCatalogue.DeliveryHour
                              + ":00, the same lot always on the same hour");
        var s = new DealerState();
        var y = BizYard("D~1", 4, 1000);
        var z = BizYard("D~2", 0, 1000);
        var unlaid = new DealerYard { Place = "D~3" };
        s.Yards.Add(y);
        s.Yards.Add(z);
        s.Yards.Add(unlaid);
        Check(s.Room(y) == 2 && s.Room(z) == 6 && s.Room(unlaid) == 0, "dealer deliveries: a lot's room is its free places");
        s.Deliveries.Add(new DealerDelivery { Id = 1, Place = "D~1", Type = "x", Due = 300 });
        s.Deliveries.Add(new DealerDelivery { Id = 2, Place = "D~1", Type = "x", Due = 250 });
        s.Deliveries.Add(new DealerDelivery { Id = 3, Place = "D~2", Type = "x", Due = 260 });
        Check(s.Room(y) == 0 && s.Inbound("D~1") == 2 && s.Room(z) == 5 && s.Inbound("D~9") == 0, "dealer deliveries: a place is held on the lot for each car on its way to it");
        var due = new List<DealerDelivery>();
        DealerRules.Due(s, 259, due);
        Check(due.Count == 1 && due[0].Id == 2, "dealer deliveries: only the cars due by the hour arrive");
        DealerRules.Due(s, 300, due);
        Check(due.Count == 3 && due[0].Id == 1 && due[2].Id == 3, "dealer deliveries: they arrive in the order they were booked");
    }

    private static void DealerReports()
    {
        var s = new DealerState();
        var a = BizYard("A~1", 2, 1000);
        var b = BizYard("B~1", 1, 1000);
        a.Cars[0].Bought = 5;
        a.Cars[1].Bought = 9;
        b.Cars[0].Bought = 2;
        s.Yards.Add(a);
        s.Yards.Add(b);
        s.Plans.Add(new DealerPlan { Id = 1, Place = "A~1", Owed = 5000, Missed = 1 });
        s.Plans.Add(new DealerPlan { Id = 2, Place = "A~1", Owed = 3000, Missed = 0 });
        s.Plans.Add(new DealerPlan { Id = 3, Place = "B~1", Owed = 2000, Missed = 2 });
        a.Rep = 300;
        // Day 10 closed, then day 11, then today 12 open.
        a.Today = new DealerBook { Day = 10, Visitors = 20, Sold = 2, Revenue = 40000, Margin = 3000, Extras = 400, Spent = 100, Takings = 40400 };
        DealerRules.CloseDay(s, a, 11);
        Check(a.Past[0].Day == 10 && a.Past[0].Cars == 2 && a.Past[0].Age == (10 - 5) + (10 - 9) && a.Past[0].Arrears == 5000 && a.Past[0].Late == 1 && a.Past[0].Rep == 300 && a.Today.Day == 11,
            "dealer reports: a day closes with its lot, its days on the lot, its arrears and its reputation read off");
        a.Today.Visitors = 10;
        a.Today.Sold = 1;
        a.Today.Margin = -500;
        a.Today.Revenue = 9000;
        DealerRules.CloseDay(s, a, 12);
        a.Today.Visitors = 4;
        b.Today = new DealerBook { Day = 12, Visitors = 6, Sold = 1, Revenue = 15000, Margin = 1200 };
        b.Past[0].CopyFrom(new DealerBook { Day = 11, Visitors = 8, Margin = 0, Cars = 1, Age = 9 });
        var day = new DealerBook();
        Check(DealerReport.Day(s, null, 11, 12, day) && day.Visitors == 18 && day.Sold == 1 && day.Margin == -500 && day.Revenue == 9000,
            "dealer reports: a day of all the showrooms is their figures added");
        Check(DealerReport.Day(s, null, 12, 12, day) && day.Visitors == 10 && day.Cars == 3 && day.Age == (12 - 5) + (12 - 9) + (12 - 2) && day.Arrears == 7000 && day.Late == 2,
            "dealer reports: today is read as it stands, its lot and arrears now");
        Check(DealerReport.Day(s, "B~1", 12, 12, day) && day.Visitors == 6 && day.Arrears == 2000 && !DealerReport.Day(s, null, 9, 12, day) && !DealerReport.Day(s, "Z~9", 12, 12, day),
            "dealer reports: one showroom's day is its own; a day nobody has figures of is none");
        var conv = new DealerBook { Visitors = 30, Sold = 2, Cars = 4, Age = 10 };
        Check(DealerReport.Value(conv, ReportKind.Conversion) == 67 && DealerReport.Value(conv, ReportKind.StockAge) == 3 && DealerReport.Value(new DealerBook(), ReportKind.Conversion) == 0
              && DealerReport.Value(new DealerBook(), ReportKind.StockAge) == 0 && DealerReport.Money(ReportKind.Profit) && !DealerReport.Money(ReportKind.Sold),
            "dealer reports: the share who bought is in tenths of a per cent and the stock's age an average, nothing over nothing is 0");
        var values = new long[DealerCatalogue.ReportDays];
        var have = new bool[DealerCatalogue.ReportDays];
        var avg = new long[DealerCatalogue.ReportDays];
        int first = DealerReport.Series(s, null, ReportKind.Visitors, 12, DealerCatalogue.ReportDays, values, have, day);
        int n = DealerCatalogue.ReportDays;
        Check(first == 12 - n + 1 && have[n - 1] && have[n - 2] && have[n - 3] && !have[n - 4] && values[n - 1] == 10 && values[n - 2] == 18 && values[n - 3] == 20,
            "dealer reports: the series is one value a day up to today, the oldest first, with the days that have none marked");
        DealerReport.Average(values, have, n, avg);
        Check(avg[n - 1] == (10 + 18 + 20 + 1) / 3 && avg[n - 3] == 20 && avg[0] == 0, "dealer reports: the line is the average of the days with figures among the last " + DealerCatalogue.AverageDays);
        var sum = new DealerBook();
        int days = DealerReport.Sum(s, null, 10, 12, 12, sum, day);
        Check(days == 3 && sum.Visitors == 48 && sum.Sold == 2 + 1 + 1 && sum.Revenue == 40000 + 9000 + 15000 && sum.Margin == 3000 - 500 + 1200 && sum.Extras == 400 && sum.Spent == 100,
            "dealer reports: the sums of a stretch of days are the days' figures added (" + sum.Visitors + " visitors, " + sum.Margin + " margin)");
        int week = DealerReport.Sum(s, "A~1", 12 - DealerCatalogue.WeekBooks + 1, 11, 12, sum, day);
        Check(week == 2 && sum.Sold == 3 && sum.Margin == 2500, "dealer reports: a showroom's week before today is its own closed days");
        // The days kept roll: the oldest goes out once there are more than the report keeps.
        var r = new DealerYard { Place = "Roll~1" };
        r.Today.Clear(0);
        for (int d = 1; d <= DealerCatalogue.ReportDays + 5; d++)
        {
            r.Today.Visitors = d;
            DealerRules.CloseDay(null, r, d);
        }
        Check(r.Past[0].Day == DealerCatalogue.ReportDays + 4 && r.Past[DealerCatalogue.ReportDays - 1].Day == 5 && r.Past[0].Visitors == DealerCatalogue.ReportDays + 5,
            "dealer reports: " + DealerCatalogue.ReportDays + " days are kept, the oldest out first");
    }

    private static void DealerSaveTwo()
    {
        var s = new DealerState { Seed = 8, FirstDay = 1, LastHour = 300, LastDay = 12, NextId = 40, MarketDay = 12, NoBidsUntil = -1 };
        var y = BizYard("Main%20St~3", 2, 820);
        y.Extras = false;
        y.Rep = 333;
        y.Done = 5;
        DealerRules.Book(y, DealerCatalogue.FeaturedKind, 3, 11);
        y.EventLast[1] = 7;
        y.Tally[DealerCatalogue.TallySold] = 9;
        y.Tally[DealerCatalogue.TallyLot] = 4;
        y.Cars[1].ShopUntil = 310;
        y.Deals.Add(new DealerOffer { Id = 7, Car = 100, Customer = "Rosa Lambert", Offer = 20000, Ceiling = 21000, Expires = 320, Extras = 9, Lift = 120 });
        y.Today = new DealerBook { Day = 12, Visitors = 5, Sold = 1, Revenue = 21000, Extras = 600, Spent = 2000, Margin = 1500 };
        y.Past[0].CopyFrom(new DealerBook { Day = 11, Visitors = 7, Cars = 2, Age = 6, Arrears = 4000, Late = 1, Rep = 320, Revenue = 9000 });
        y.Past[1].CopyFrom(new DealerBook { Day = 10, Visitors = 3 });
        s.Yards.Add(y);
        s.Sales.Add(new DealerSale { Day = 12, Place = y.Place, Customer = "Ben Osei", Type = BizModels[1].Type, Price = 21000, Cost = 18000, Tier = -1, Extras = 6 });
        s.Deliveries.Add(new DealerDelivery { Id = 30, Place = y.Place, Type = BizModels[2].Type, Colour = "Red", Km = 52000, Condition = 710, Faults = 3, Known = 1, Cost = 24000, Due = 344, Told = true });
        string line = DealerStore.Write(s);
        DealerState back = DealerStore.Read(line, out int version);
        Check(back != null && version == 2 && DealerStore.Version == 2 && DealerStore.Write(back) == line, "dealer save: the second version's line reads back to exactly what was written");
        DealerYard by = back != null && back.Yards.Count == 1 ? back.Yards[0] : null;
        Check(by != null && !by.Extras && by.Rep == 333 && by.Done == 5 && by.Event == DealerCatalogue.FeaturedKind && by.Feature == 3 && by.EventFrom == 11 && by.EventUntil == y.EventUntil
              && by.EventLast[1] == 7 && by.EventLast[0] == -1 && by.Tally[DealerCatalogue.TallySold] == 9 && by.Tally[DealerCatalogue.TallyLot] == 4 && by.Cars[1].ShopUntil == 310 && by.Cars[0].ShopUntil == -1
              && by.Deals[0].Extras == 9 && by.Deals[0].Lift == 120 && by.Today.Spent == 2000 && by.Past[0].Arrears == 4000 && by.Past[0].Rep == 320 && by.Past[1].Day == 10 && by.Past[2].Day == -1
              && back.Sales[0].Extras == 6 && back.Deliveries.Count == 1 && back.Deliveries[0].Due == 344 && back.Deliveries[0].Told && back.Deliveries[0].Known == 1 && back.Deliveries[0].Colour == "Red",
            "dealer save: the event, reputation, missions, tallies, workshop, extras, books and cars on their way all come back");
        Check(line.Length < 6000, "dealer save: a showroom's line stays small (" + line.Length + " characters)");

        // A line the first version wrote: it loads as before, with none of what came later.
        const string one = "v=1^h:77|3|400|16|90|16|18^y:Main%20St~12|Marks Motors|930|0|1|456^p:-120|5|300|90;220|5|300|90"
                           + "^c:4|Ab+c/9==|ba:vehicletype_bima320|Red|41000|760|5|1|18000|23450|12|1|1|401"
                           + "^d:7|4|Rosa Lambert|2|22000|23900|1|390|414|1|1|1|1|700|20|120|1|ba:vehicletype_honzamimic|Black|90000|600|3100"
                           + "^b:16|9|7|2|3|1|23000|18000|300|4000;15|4|0|0|0|0|0|0|0|0;14|2|0|0|0|0|0|0|0|0"
                           + "^m:^f:60|Main%20St~12|Ben Osei|ba:vehicletype_bima320|20000|9000|640|21|1|3200|650^w:^s:14|Main%20St~12|Ben Osei|ba:vehicletype_bima320|24000|19000|1|2|0";
        DealerState old = DealerStore.Read(one, out version);
        DealerYard oy = old != null && old.Yards.Count == 1 ? old.Yards[0] : null;
        Check(old != null && version == 1 && oy != null && oy.Name == "Marks Motors" && oy.StaffFloor == 930 && !oy.Finance && oy.VisitorCarry == 456 && oy.Cars.Count == 1 && oy.Cars[0].OutUntil == 401
              && oy.Deals.Count == 1 && oy.Deals[0].TradeAllowance == 3100 && oy.Today.Visitors == 9 && oy.Past[0].Day == 15 && oy.Past[1].Day == 14 && old.Plans.Count == 1 && old.Sales[0].Tier == 2,
            "dealer save: a first version's line loads as it did");
        Check(oy != null && oy.Extras && oy.Rep == 0 && oy.Done == 0 && oy.Event == -1 && oy.EventLast[0] == -1 && oy.Tally[0] == 0 && oy.Cars[0].ShopUntil == -1 && oy.Deals[0].Extras == 0 && oy.Deals[0].Lift == 0
              && oy.Past[0].Cars == 0 && oy.Past[2].Day == -1 && old.Deliveries.Count == 0 && old.Sales[0].Extras == 0,
            "dealer save: in a first version's line the showroom sells extras and has no reputation, event, mission, workshop or car on its way yet");
        DealerState again = DealerStore.Read(DealerStore.Write(old), out version);
        Check(again != null && version == 2 && DealerStore.Write(again) == DealerStore.Write(old), "dealer save: a first version's line is written as the second from then on and reads back the same");
    }

    // Every text the business's rules name, by a key built from a number, is in en.json (locales.py holds pt and zh-cn to the same keys).
    private static void DealerBizTexts()
    {
        string locale = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales\en.json"));
        Dictionary<string, string> words = System.IO.File.Exists(locale)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(locale).TrimStart('﻿')) : null;
        Check(words != null, "dealer texts: en.json is where the checks look for it (" + locale + ")");
        if (words == null)
        {
            return;
        }
        var want = new List<string>();
        for (int k = 0; k < DealerCatalogue.EventKinds; k++)
        {
            want.Add("reallife_dealer_event_" + k);
            want.Add("reallife_dealer_event_" + k + "_short");
            want.Add("reallife_dealer_event_" + k + "_about");
        }
        for (int m = 0; m < DealerCatalogue.Missions; m++)
        {
            want.Add("reallife_dealer_mission_" + m);
            want.Add("reallife_dealer_mission_" + m + "_about");
        }
        for (int s = 0; s < DealerCatalogue.RepSteps.Length; s++)
        {
            want.Add("reallife_dealer_step_" + s);
            if (s > 0)
            {
                want.Add("reallife_dealer_perk_" + s);
            }
        }
        for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
        {
            want.Add("reallife_dealer_addon_" + e);
            want.Add(DealerLines.ExtraKeys[e]);
            want.Add(DealerLines.ExtraKeys[e] + "_label");
        }
        for (int k = 1; k <= 6; k++)
        {
            want.Add("reallife_dealer_carkind_" + k);
        }
        foreach (string key in new[] { DealerLines.EventKey, DealerLines.WorkshopKey, DealerLines.ExtrasCostKey })
        {
            want.Add(key);
            want.Add(key + "_label");
        }
        var missing = new List<string>();
        foreach (string key in want)
        {
            if (!words.ContainsKey(key))
            {
                missing.Add(key);
            }
        }
        Check(missing.Count == 0, "dealer texts: every event, mission, step, perk, extra, kind of car and money line has its text (missing: " + string.Join(", ", missing) + ")");
        bool filled = words.TryGetValue("reallife_dealer_mission_8_about", out string quick) && quick.Contains("{target}") && quick.Contains("{days}")
                      && words.TryGetValue("reallife_dealer_mission_10_about", out string margin) && margin.Contains("{share}");
        Check(filled, "dealer texts: the missions' texts name what the code fills in");
    }

    // An hour of a full showroom with an event and every perk, and a report of eight showrooms' sixty days, are cheap.
    private static void DealerBizTiming()
    {
        var s = new DealerState { Seed = 12 };
        var y = BizYard("T~1", 6, 900);
        for (int c = 6; c < DealerCatalogue.CarsMost; c++)
        {
            y.Spots.Add(new LotSpot());
            y.Cars.Add(new StockCar { Id = 200 + c, Type = BizModels[c % 5].Type, Asking = 30000, Spot = c, Condition = 900 });
        }
        y.Rep = DealerCatalogue.RepMost;
        DealerRules.Book(y, DealerCatalogue.FeaturedKind, 2, 1);
        s.Yards.Add(y);
        var worths = new List<int>();
        for (int c = 0; c < y.Cars.Count; c++)
        {
            worths.Add(28000);
        }
        var hour = new YardHour { Open = true, ShopCustomers = 60f, Promotion = 80, BaseMultiplier = 0.5f, Satisfaction = 80, Staff = 6, Skill = 90 };
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            DealerRules.Hour(s, y, hour, 24 + 12 + (i % 2), 1, worths, 5000, BizModels, DealerColours, 1000);
            y.Deals.Clear();
        }
        watch.Stop();
        double each = watch.Elapsed.TotalMilliseconds / 1000.0;
        Check(each < 0.2, "dealer timing: an hour of a full showroom with an event and every perk takes under 0.2 ms (" + each.ToString("0.0000") + " ms)");

        var r = new DealerState();
        for (int k = 0; k < DealerCatalogue.YardsMost; k++)
        {
            var yard = new DealerYard { Place = "Y~" + k };
            for (int d = 0; d < DealerCatalogue.ReportDays; d++)
            {
                yard.Past[d].CopyFrom(new DealerBook { Day = 99 - d, Visitors = d, Sold = 1, Margin = 100 });
            }
            yard.Today.Clear(100);
            r.Yards.Add(yard);
        }
        var values = new long[DealerCatalogue.ReportDays];
        var have = new bool[DealerCatalogue.ReportDays];
        var avg = new long[DealerCatalogue.ReportDays];
        var scratch = new DealerBook();
        watch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            DealerReport.Series(r, null, (ReportKind)(i % (int)ReportKind.Count), 100, DealerCatalogue.ReportDays, values, have, scratch);
            DealerReport.Average(values, have, DealerCatalogue.ReportDays, avg);
        }
        watch.Stop();
        each = watch.Elapsed.TotalMilliseconds / 100.0;
        DealerReport.Series(r, null, ReportKind.Profit, 100, DealerCatalogue.ReportDays, values, have, scratch);
        Check(each < 2.0 && values[DealerCatalogue.ReportDays - 2] == 100 * DealerCatalogue.YardsMost, "dealer timing: a report of " + DealerCatalogue.YardsMost + " showrooms' " + DealerCatalogue.ReportDays
                                                                                               + " days takes under 2 ms (" + each.ToString("0.000") + " ms), and adds them up");
    }
}
