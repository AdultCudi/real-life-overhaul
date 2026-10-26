using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Hustles;

// Legal hustles (Scripts/Hustles): the Marketplace's catalogue, the morning's listings and requests, worth, haggling,
// repairs and cleaning, buyers and their counters, the hour, freelance visits and their stars, helpers, the save's line,
// a year of flipping and of visits against the jobs' pay, the timing, the layering and the texts in every language.
internal static partial class Program
{
    private static readonly List<MarketModel> HustleCars = new List<MarketModel>
    {
        new MarketModel("ba:vehicletype_honzamimic", 9000f, 1), new MarketModel("ba:vehicletype_bima320", 28000f, 2),
        new MarketModel("ba:vehicletype_vordv150", 34000f, 5), new MarketModel("ba:vehicletype_mersaidis500", 90000f, 3)
    };
    private static readonly List<string> HustleColours = new List<string> { "White", "Black", "Red" };

    private static void Hustles()
    {
        HustleCatalogueChecks();
        HustleMorningChecks();
        HustleBuyingChecks();
        HustleRepairChecks();
        HustleSellingChecks();
        HustleHourChecks();
        HustleVisitChecks();
        HustleStoreChecks();
        HustlePickupChecks();
        HustleBalanceChecks();
        HustleTiming();
        HustleSource();
        HustleTexts();
    }

    private static List<HustlePlace> HustleHomes(int count)
    {
        var homes = new List<HustlePlace>(count);
        for (int i = 0; i < count; i++)
        {
            homes.Add(new HustlePlace(i % 3 == 0 ? "1st Avenue" : i % 3 == 1 ? "Broad Street" : "Elm Road", 10 + i));
        }
        return homes;
    }

    private static HustleState HustleFresh(int seed)
    {
        return new HustleState { Seed = seed };
    }

    // ---------------------------------------------------------------- the catalogue

    private static void HustleCatalogueChecks()
    {
        bool things = true, ids = true;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ThingDef d in HustleCatalogue.Things)
        {
            things &= d.NewPrice > 0 && d.FixMinutes > 0 && d.FixMost > 0 && d.FixMost <= 100 && d.CleanGain >= 0 && d.NameKey == "reallife_hustle_thing_" + d.Id;
            things &= d.Kind != ThingKind.Electronics ? d.CleanGain > 0 && !d.ShopFix : d.CleanGain == 0 && d.ShopFix;
            ids &= seen.Add(d.Id) && HustleCatalogue.Thing(d.Id) == d;
        }
        Check(things && ids && HustleCatalogue.Things.Length == 16, "hustles: sixteen kinds of goods, each with a price, a repair, its name's key, electronics to the repair shop and the rest to be cleaned");
        bool gigs = HustleCatalogue.Gigs.Length == 4;
        for (int i = 0; i < HustleCatalogue.Gigs.Length; i++)
        {
            GigDef g = HustleCatalogue.Gigs[i];
            gigs &= (int)g.Kind == i && g.Pay > 0 && g.Minutes > 0 && g.Weight > 0 && (g.Skill != null) == (g.Kind != GigKind.Handyman) && HustleCatalogue.Gig(g.Kind) == g;
            int hourly = g.Pay * 60 / (g.Minutes * (g.TwoStops ? 2 : 1));
            gigs &= hourly >= 60 && hourly <= 130;   // above a gig on foot (40 to 55), about a gig with a car (100)
        }
        Check(gigs, "hustles: four kinds of visit, in their order, each paying 60 to 130 an hour of work");
        Check(HustleCatalogue.Named(3) == 5 && HustleCatalogue.Named(152) == 150 && HustleCatalogue.Named(157) == 155 && HustleCatalogue.Named(456) == 460
              && HustleCatalogue.Named(1024) == 1000 && HustleCatalogue.Named(1026) == 1050, "hustles: prices are named in fives, tens and fifties");
    }

    // ---------------------------------------------------------------- the morning

    private static void HustleMorningChecks()
    {
        List<HustlePlace> homes = HustleHomes(40);
        HustleState a = HustleFresh(777), b = HustleFresh(777), c = HustleFresh(777);
        HustleRules.Morning(a, 12, homes, HustleCars, HustleColours, 1000);
        HustleRules.Morning(b, 12, homes, HustleCars, HustleColours, 1000);
        HustleRules.Morning(c, 13, homes, HustleCars, HustleColours, 1000);
        Check(HustleStore.Write(a) == HustleStore.Write(b), "hustles: the same save and day draw the same listings and requests");
        Check(HustleStore.Write(a) != HustleStore.Write(c), "hustles: the next day draws others");
        int cars = 0;
        foreach (Listing l in a.Listings)
        {
            cars += l.Car ? 1 : 0;
        }
        Check(a.Listings.Count == HustleCatalogue.GoodsADay + HustleCatalogue.CarsADay && cars == HustleCatalogue.CarsADay && a.Gigs.Count == HustleCatalogue.GigsADay,
            "hustles: a morning has " + HustleCatalogue.GoodsADay + " goods, " + HustleCatalogue.CarsADay + " cars and " + HustleCatalogue.GigsADay + " requests (" + a.Listings.Count + ", " + cars + ", "
            + a.Gigs.Count + ")");
        bool bounds = true, places = true, overstate = true, gigs = true;
        int said = 0, goods = 0;
        for (int day = 1; day <= 300; day++)
        {
            HustleState s = HustleFresh(4242);
            HustleRules.Morning(s, day, homes, HustleCars, HustleColours, 1000);
            foreach (Listing l in s.Listings)
            {
                places &= homes.Exists(h => h.Is(l.Street, l.Number)) && !string.IsNullOrEmpty(l.Seller);
                bounds &= l.Floor <= l.Asking && l.Floor > 0 && l.Condition >= 1 && l.Condition <= 100 && l.Shown >= l.Condition && l.Shown <= 100;
                if (l.Car)
                {
                    continue;
                }
                goods++;
                ThingDef def = HustleCatalogue.Thing(l.Thing);
                int worth = HustleRules.GoodsWorth(def, l.Condition, 1000);
                bounds &= def != null && l.Condition >= HustleCatalogue.ListingConditionLeast && l.Condition <= HustleCatalogue.ListingConditionMost;
                bounds &= l.Asking >= HustleCatalogue.Share(worth, HustleCatalogue.AskingLeast) - 30 && l.Asking <= HustleCatalogue.Share(worth, HustleCatalogue.AskingMost) + 30;
                if (l.Shown > l.Condition)
                {
                    said++;
                    overstate &= def.Kind == ThingKind.Electronics && l.Shown - l.Condition <= HustleCatalogue.OverstateMost;
                }
            }
            foreach (Gig g in s.Gigs)
            {
                GigDef def = HustleCatalogue.Gig(g.Kind);
                gigs &= g.Stage == GigStage.Offered && g.Until - g.From == HustleCatalogue.GigWindow && g.From / 24 == day
                        && g.From % 24 >= HustleCatalogue.GigFromLeast && g.From % 24 <= HustleCatalogue.GigFromMost && g.Pay > 0;
                gigs &= def.TwoStops ? g.Street2 != null && !(g.Street2 == g.Street && g.Number2 == g.Number) : g.Street2 == null;
            }
        }
        Check(places, "hustles: every listing is at one of the city's homes, with a seller");
        Check(bounds, "hustles: a listing's condition, what is said of it, its asking price (82 to 130 % of its worth) and the seller's least are in their bounds");
        Check(overstate && said > goods / 20 && said < goods / 5, "hustles: only electronics sellers say a thing is better than it is, now and then (" + said + " of " + goods + ")");
        Check(gigs, "hustles: a request is wanted from 9 to 16 the same day, waits four hours, and a move has a second, other address");
        HustleState kept = HustleFresh(5);
        HustleRules.Morning(kept, 3, homes, HustleCars, HustleColours, 1000);
        kept.Gigs[0].Stage = GigStage.Booked;
        kept.Gigs[1].Stage = GigStage.Done;
        Listing old = kept.Listings[0];
        HustleRules.Morning(kept, 4, homes, HustleCars, HustleColours, 1000);
        Check(kept.GigOf(kept.Gigs[0].Id).Stage == GigStage.Booked && kept.Gigs.FindAll(g => g.Stage == GigStage.Booked).Count == 1
              && kept.Gigs.FindAll(g => g.Stage == GigStage.Done).Count == 0 && kept.ListingOf(old.Id) == null,
            "hustles: the next morning keeps a booked visit and drops yesterday's listings, requests and visits done");
        Check(HustleRules.GigsToday(0, 0) == 3 && HustleRules.GigsToday(4600, 5) == 4 && HustleRules.GigsToday(2900, 5) == 2 && HustleRules.GigsToday(3500, 5) == 3,
            "hustles: three requests a day, four for a rating of 4.5 or more, two under 3");
        HustleState none = HustleFresh(9);
        HustleRules.Morning(none, 2, new List<HustlePlace>(), HustleCars, HustleColours, 1000);
        Check(none.Listings.Count == 0 && none.Gigs.Count == 0 && none.Day == 2, "hustles: a city with no homes has nothing for sale and no requests, and the day still turns");
    }

    // ---------------------------------------------------------------- buying

    private static void HustleBuyingChecks()
    {
        var l = new Listing { Id = 1, Thing = "laptop", Condition = 40, Shown = 40, Asking = 400, Floor = 340, Seller = "A", Street = "S", Number = 1 };
        HaggleAnswer a1 = HustleRules.Haggle(l, 300, 0, out int c1);
        HaggleAnswer a2 = HustleRules.Haggle(l, 300, 0, out int c2);
        HaggleAnswer a3 = HustleRules.Haggle(l, 300, 0, out _);
        Check(a1 == HaggleAnswer.Countered && a2 == HaggleAnswer.Countered && c2 <= c1 && c1 >= 340 && c1 < 400 && a3 == HaggleAnswer.WalkedAway && l.Gone,
            "hustles: a seller counters at their least and a share above it, never higher the next time, and after three offers sells elsewhere (" + c1 + ", " + c2 + ")");
        var m = new Listing { Asking = 400, Floor = 340 };
        Check(HustleRules.Haggle(m, 340, 0, out _) == HaggleAnswer.Taken, "hustles: an offer at the seller's least is taken");
        var n = new Listing { Asking = 400, Floor = 340 };
        HustleRules.Haggle(n, 300, 0, out int counter);
        Check(HustleRules.Haggle(n, counter, 0, out _) == HaggleAnswer.Taken, "hustles: their own counter is taken");
        Check(HustleRules.FloorFor(m, 100) == 340 - HustleCatalogue.Share(400, HustleCatalogue.NegotiationLever) && HustleRules.FloorFor(m, 0) == 340
              && HustleRules.FloorFor(new Listing { Asking = 400, Floor = 260 }, 100) >= HustleCatalogue.Share(400, HustleCatalogue.HaggleLeast),
            "hustles: negotiation skill brings a seller's least down by up to 6 % of the asking price, never under the least offer allowed");
        Check(HustleRules.HaggleAt(400, 0) == 260 && HustleRules.HaggleAt(400, HustleRules.HaggleSteps - 1) == 400, "hustles: offers run from 65 % to all of the asking price");
        var s = HustleFresh(3);
        var buy = new Listing { Id = s.Next(), Thing = "sofa", Condition = 30, Shown = 30, Asking = 200, Floor = 170, Seller = "B", Street = "Elm Road", Number = 4 };
        s.Listings.Add(buy);
        int fee = HustleRules.DeliveryFee(200, 1000);
        Owned o = HustleRules.Buy(s, buy, 180, true, fee, 7);
        Check(s.Listings.Count == 0 && s.Items.Count == 1 && o.State == OwnedState.Delivering && o.Due == 8 && o.Spent == fee && o.Paid == 180 && fee == HustleCatalogue.Named(25 + 10),
            "hustles: goods bought to be brought home leave the listings, cost the fee and come the next day");
        var car = new Listing { Id = s.Next(), Car = true, Thing = "ba:vehicletype_bima320", Condition = 70, Shown = 70, Asking = 9000, Floor = 8500, Street = "S", Number = 2 };
        s.Listings.Add(car);
        Owned oc = HustleRules.Buy(s, car, 9000, true, 0, 7);
        Check(oc.State == OwnedState.Waiting && oc.Car && oc.Spent == 0, "hustles: a car is always collected at the seller's door");
        int w0 = HustleRules.GoodsWorth(HustleCatalogue.Thing("laptop"), 0, 1000), w50 = HustleRules.GoodsWorth(HustleCatalogue.Thing("laptop"), 50, 1000),
            w100 = HustleRules.GoodsWorth(HustleCatalogue.Thing("laptop"), 100, 1000), w2 = HustleRules.GoodsWorth(HustleCatalogue.Thing("laptop"), 100, 2000);
        Check(w0 > 0 && w0 < w50 && w50 < w100 && w100 == 770 && w2 == 1540, "hustles: goods are worth more in better condition (" + w0 + ", " + w50 + ", " + w100 + ") and follow the price level");
        int clean = HustleRules.CarWorth(28000, 1000, 60000, 80, 0), dirty = HustleRules.CarWorth(28000, 1000, 60000, 80, 100);
        Check(clean == DealerRules.Worth(28000, 1000, 60000, 800) && dirty == HustleCatalogue.Share(clean, 900),
            "hustles: a car is worth what the showroom says it is, and up to a tenth less dirty");
    }

    // ---------------------------------------------------------------- fixing and cleaning

    private static void HustleRepairChecks()
    {
        Check(HustleRules.FixChance(0, 50) == 450 && HustleRules.FixChance(100, 50) == 950 && HustleRules.FixChance(0, 10) == 300 && HustleRules.FixChance(-5, 1) >= HustleCatalogue.FixChanceLeast,
            "hustles: a repair by hand works 45 % of the time with no experience, 95 % with all of it, 15 % less for a wreck");
        int tried = 0, worked = 0;
        bool bounds = true, same = true;
        for (int i = 0; i < 4000; i++)
        {
            var s = HustleFresh(1000 + i % 7);
            s.RepairXp = 20;
            var o = new Owned { Id = i, Thing = "camera", Condition = 40 };
            var copy = new Owned { Id = i, Thing = "camera", Condition = 40 };
            var s2 = HustleFresh(1000 + i % 7);
            s2.RepairXp = 20;
            bool w = HustleRules.FixByHand(s, o, 300 + i);
            same &= HustleRules.FixByHand(s2, copy, 300 + i) == w && copy.Condition == o.Condition && s2.RepairXp == s.RepairXp;
            tried++;
            worked += w ? 1 : 0;
            bounds &= w ? o.Condition >= HustleCatalogue.Thing("camera").FixMost - 8 && o.Condition <= HustleCatalogue.Thing("camera").FixMost && s.RepairXp == 26
                : o.Condition >= 40 - HustleCatalogue.FixMiss && o.Condition < 40 && s.RepairXp == 23;
        }
        int expected = HustleRules.FixChance(20, 40);
        Check(Math.Abs(worked * 1000 / tried - expected) <= 30, "hustles: repairs by hand work as often as their chance says (" + worked * 1000 / tried + " of " + expected + " thousandths)");
        Check(bounds, "hustles: a repair that works brings the thing to its kind's best less up to 8, one that fails costs up to 6; experience grows either way");
        Check(same, "hustles: the same attempt at a repair always comes out the same");
        var full = new Owned { Thing = "camera", Condition = 95 };
        Check(!HustleRules.CanFix(full) && HustleRules.CanFix(new Owned { Thing = "camera", Condition = 30 }) && !HustleRules.CanFix(new Owned { Car = true, Thing = "x", Condition = 10 }),
            "hustles: nothing at its best is mended again, and a car is mended the game's way");
        ThingDef tv = HustleCatalogue.Thing("television");
        Check(HustleRules.ShopFee(tv, 10, 1000) > HustleRules.ShopFee(tv, 60, 1000) && HustleRules.ShopFee(tv, 99, 1000) == HustleCatalogue.ShopLeast,
            "hustles: a repair shop charges more for worse, never under " + HustleCatalogue.ShopLeast);
        var r = new Owned { Thing = "television", Condition = 30 };
        HustleRules.ShopFixed(r);
        Check(r.Condition == HustleCatalogue.ShopCondition && HustleRules.CanShop(new Owned { Thing = "phone", Condition = 50 }) && !HustleRules.CanShop(new Owned { Thing = "sofa", Condition = 50 }),
            "hustles: a repair shop takes electronics only, and gives them back at 92 %");
        var j = new Owned { Thing = "jacket", Condition = 60 };
        bool can = HustleRules.CanClean(j);
        HustleRules.Clean(j);
        int once = j.Condition;
        HustleRules.Clean(j);
        var high = new Owned { Thing = "jacket", Condition = 90 };
        HustleRules.Clean(high);
        Check(can && once == 78 && j.Condition == 78 && !HustleRules.CanClean(j) && high.Condition == HustleCatalogue.CleanMost && !HustleRules.CanClean(new Owned { Thing = "phone", Condition = 40 }),
            "hustles: a clean adds the kind's gain once, never past 95 %, and electronics are not cleaned");
    }

    // ---------------------------------------------------------------- selling

    private static void HustleSellingChecks()
    {
        bool falls = true;
        int last = int.MaxValue;
        for (int ask = 500; ask <= 1800; ask += 25)
        {
            int c = HustleRules.OfferChance(ask, 1000, 70, false, 12);
            falls &= c <= last;
            last = c;
        }
        Check(falls && HustleRules.OfferChance(1000, 1000, 70, false, 12) > HustleRules.OfferChance(1000, 1000, 20, false, 12)
              && HustleRules.OfferChance(1000, 1000, 70, false, 7) == 0 && HustleRules.OfferChance(1000, 1000, 70, false, 22) == 0
              && HustleRules.OfferChance(1000, 1000, 70, true, 12) < HustleRules.OfferChance(1000, 1000, 70, false, 12) && HustleRules.OfferChance(1000, 0, 70, false, 12) == 0,
            "hustles: buyers write in less for a dearer or worse thing, only from 8 to 22, and less often for a car");
        bool offers = true;
        for (int i = 0; i < 2000; i++)
        {
            var dice = new DealerDice(11, i, 3);
            var o = new Owned { Id = i, Thing = "boots", Asking = 100 + i % 500 };
            int worth = 80 + i % 600;
            BuyerOffer d = HustleRules.MakeOffer(i, o, worth, 50, ref dice);
            offers &= d.Offer > 0 && d.Offer <= o.Asking && d.Ceiling >= d.Offer && d.Ceiling <= o.Asking && d.Expires == 50 && !string.IsNullOrEmpty(d.Buyer);
        }
        Check(offers, "hustles: a buyer offers under the asking price, with a ceiling at or above it and never above the asking price");
        var b = new BuyerOffer { Id = 4, Offer = 400, Ceiling = 500 };
        Check(HustleRules.AcceptChance(b, 400) == 1000 && HustleRules.AcceptChance(b, 500) == HustleCatalogue.AcceptAtCeiling && HustleRules.AcceptChance(b, 501) == 0
              && HustleRules.AcceptChance(b, 450) > HustleRules.AcceptChance(b, 480), "hustles: a counter at the buyer's offer is certain, at their ceiling seldom, above it never");
        var x = new BuyerOffer { Id = 9, Offer = 400, Ceiling = 450 };
        var y = new BuyerOffer { Id = 9, Offer = 400, Ceiling = 450 };
        bool tx = HustleRules.Counter(x, 440, 77, out bool gx), ty = HustleRules.Counter(y, 440, 77, out bool gy);
        Check(tx == ty && gx == gy && x.Offer == y.Offer, "hustles: the same counter always gets the same answer");
        var z = new BuyerOffer { Id = 12, Offer = 400, Ceiling = 420 };
        HustleRules.Counter(z, 600, 1, out bool g1);
        int up = z.Offer;
        HustleRules.Counter(z, 600, 1, out bool g2);
        Check(!g1 && up > 400 && up <= 420 && g2, "hustles: a refused counter brings the buyer up, never past their ceiling, and two send them away");
        Check(HustleRules.CounterAt(b, 600, 0) == 400 && HustleRules.CounterAt(b, 600, HustleRules.CounterSteps(b, 600) - 1) == 600 && HustleRules.CounterSteps(b, 600) <= 11,
            "hustles: counters run from the buyer's offer to the asking price in up to eleven steps");
        Check(HustleRules.AskAt(1000, 0) == 700 && HustleRules.AskAt(1000, HustleRules.AskSteps - 1) == 1500 && HustleRules.AskAt(1000, HustleRules.AskStepAtWorth) == 1000
              && HustleRules.AskStepOf(1000, 1100) == HustleRules.AskStepAtWorth + 2, "hustles: the player asks 70 to 150 % of a thing's worth, starting at its worth");
        var s = HustleFresh(2);
        var item = new Owned { Id = s.Next(), Thing = "suit", Paid = 100, Spent = 20, State = OwnedState.Listed, Asking = 200 };
        s.Items.Add(item);
        s.Offers.Add(new BuyerOffer { Id = s.Next(), Item = item.Id, Offer = 180 });
        int margin = HustleRules.Sell(s, item, "C", 180, 9);
        Check(margin == 60 && s.Items.Count == 0 && s.Offers.Count == 0 && s.Sales.Count == 1 && s.Profit == 60 && s.Sales[0].Cost == 120,
            "hustles: a sale takes the thing and its offers, keeps the sale and counts the margin over what was paid and spent");
        var t = new Owned { Id = s.Next(), Thing = "suit", Paid = 0, Test = true };
        s.Items.Add(t);
        HustleRules.Sell(s, t, "D", 500, 9);
        Check(s.Profit == 60, "hustles: a test's sale leaves the player's profit as it was");
        for (int i = 0; i < 20; i++)
        {
            s.Sold(new Sale { Thing = "suit", Day = i });
        }
        Check(s.Sales.Count == HustleCatalogue.SalesKept && s.Sales[0].Day == 19, "hustles: the last " + HustleCatalogue.SalesKept + " sales are kept, the latest first");
    }

    // ---------------------------------------------------------------- the hour

    private static void HustleHourChecks()
    {
        var s = HustleFresh(31);
        var events = new List<HustleEvent>();
        var home = new Owned { Id = s.Next(), Thing = "sofa", State = OwnedState.Delivering, Due = 5 };
        s.Items.Add(home);
        var worths = new List<int> { 300 };
        HustleRules.Hour(s, 5 * 24 + 7, worths, events);
        bool before = home.State == OwnedState.Delivering;
        HustleRules.Hour(s, 5 * 24 + 8, worths, events);
        Check(before && home.State == OwnedState.Kept && events.Exists(e => e.Kind == HustleEventKind.Delivered && e.Id == home.Id), "hustles: a delivery comes home at 8 on its day, and the player is told");
        var listed = new Owned { Id = s.Next(), Thing = "phone", State = OwnedState.Listed, Asking = 200, Condition = 80 };
        s.Items.Add(listed);
        worths = new List<int> { 300, 600 };
        int stamp = 6 * 24 + 9, got = -1;
        for (int h = 0; h < 37 && got < 0; h++)
        {
            events.Clear();
            HustleRules.Hour(s, stamp + h, worths, events);
            got = events.Exists(e => e.Kind == HustleEventKind.Offer) ? stamp + h : -1;
        }
        BuyerOffer offer = s.OfferFor(listed.Id);
        Check(got >= 0 && offer != null && offer.Expires == got + HustleCatalogue.OfferHours, "hustles: a thing listed well under its worth draws a buyer within two days, whose offer stands six hours");
        events.Clear();
        HustleRules.Hour(s, offer.Expires, worths, events);
        Check(s.OfferFor(listed.Id) == null || s.OfferFor(listed.Id).Id != offer.Id, "hustles: an offer nobody answered lapses");
        var g = new Gig { Id = s.Next(), Kind = GigKind.Handyman, Client = "E", Street = "S", Number = 1, Pay = 130, From = 100, Until = 104, Stage = GigStage.Booked };
        s.Gigs.Add(g);
        events.Clear();
        HustleRules.Hour(s, 103, worths, events);
        bool waiting = g.Stage == GigStage.Booked;
        HustleRules.Hour(s, 104, worths, events);
        Check(waiting && g.Stage == GigStage.Missed && g.Rating == 1 && s.Ratings.Count == 1 && s.Ratings[0] == 1 && events.Exists(e => e.Kind == HustleEventKind.Missed),
            "hustles: a booked visit nobody came to by the end of the client's wait is missed, one star");
        var h2 = new Gig { Id = s.Next(), Kind = GigKind.Junk, Client = "F", Street = "S", Number = 2, Pay = 220, From = 200, Until = 204, Stage = GigStage.Booked };
        s.Gigs.Add(h2);
        HustleRules.SendHelper(s, h2, 190);
        events.Clear();
        HustleRules.Hour(s, 201, worths, events);
        bool notYet = h2.Stage == GigStage.Helper;
        HustleRules.Hour(s, h2.HelperDone, worths, events);
        HustleEvent done = events.Find(e => e.Kind == HustleEventKind.HelperDone);
        Check(notYet && h2.HelperDone == 202 && h2.Stage == GigStage.Done && h2.HelperFee == HustleCatalogue.Named(HustleCatalogue.Share(220, 550))
              && done.Amount == 220 - h2.HelperFee + (h2.Rating >= 5 ? HustleRules.Tip(220) : 0) && h2.Rating >= 3 && h2.Rating <= 5,
            "hustles: a helper starts when the client wants, is done after the visit's hours, takes 55 % and is rated 3 to 5 for the player");
    }

    // ---------------------------------------------------------------- visits

    private static void HustleVisitChecks()
    {
        var s = HustleFresh(8);
        var g = new Gig { Id = 1, Kind = GigKind.Detailing, Client = "G", Pay = 150, From = 50, Until = 54, Stage = GigStage.Booked };
        int pay = HustleRules.Worked(s, g, 120, true, 60);
        Check(g.Stage == GigStage.Done && pay == 150 + (g.Rating >= 5 ? HustleRules.Tip(150) : 0) && s.Ratings.Count == 1 && s.JobsDone == 1 && s.Earned == pay,
            "hustles: a visit done is paid whole, a five-star client tips, and it counts towards the rating");
        var early = new Gig { Id = 2, Kind = GigKind.Detailing, Pay = 150, Stage = GigStage.Booked };
        int part = HustleRules.Worked(s, early, 60, false, 60);
        Check(part == 75 && early.Rating <= 3, "hustles: a visit left halfway is paid for half, two stars lower");
        int lateSum = 0, onTimeSum = 0;
        for (int i = 0; i < 200; i++)
        {
            var on = new Gig { Id = 100 + i, Kind = GigKind.Handyman, Pay = 100, Stage = GigStage.Booked };
            var late = new Gig { Id = 100 + i, Kind = GigKind.Handyman, Pay = 100, Stage = GigStage.Booked, Late = true };
            onTimeSum += HustleRules.Stars(s, on, 50, true);
            lateSum += HustleRules.Stars(s, late, 50, true);
        }
        Check(onTimeSum - lateSum >= 150, "hustles: coming late costs about a star (" + onTimeSum + " against " + lateSum + " over 200 visits)");
        bool range = true;
        for (int skill = 0; skill <= 100; skill += 10)
        {
            for (int i = 0; i < 50; i++)
            {
                int st = HustleRules.Stars(s, new Gig { Id = i, Late = i % 2 == 0 }, skill, i % 3 != 0);
                range &= st >= 1 && st <= 5;
            }
        }
        Check(range && HustleRules.Stars(s, new Gig { Id = 3 }, 100, true) >= HustleRules.Stars(s, new Gig { Id = 3 }, 0, true), "hustles: stars run from one to five and rise with skill");
        var move = new Gig { Id = 5, Kind = GigKind.Moving, Client = "H", Street = "A", Number = 1, Street2 = "B", Number2 = 2, Pay = 360, From = 10, Until = 14, Stage = GigStage.Booked };
        int first = HustleRules.Worked(s, move, 90, true, 50);
        HustlePlace second = HustleRules.Where(move);
        bool open = HustleRules.Open(move, 15);
        int last = HustleRules.Worked(s, move, 90, true, 50);
        Check(first == 0 && move.Stage == GigStage.Done && second.Street == "B" && second.Number == 2 && open && last >= 360,
            "hustles: a move is loaded at one address, unloaded at the other, and paid at the end");
        var m2 = new Gig { Id = 6, Kind = GigKind.Moving, Street = "A", Number = 1, Street2 = "B", Number2 = 2, Pay = 360, Stage = GigStage.Loaded };
        Check(HustleRules.Worked(s, m2, 45, false, 50) == HustleCatalogue.Share(360, 1000 * 135 / 180), "hustles: a move left halfway through the unloading is paid for the share done");
        Check(HustleRules.Open(new Gig { Stage = GigStage.Booked, From = 20, Until = 24 }, 19) && !HustleRules.Open(new Gig { Stage = GigStage.Booked, From = 20, Until = 24 }, 18)
              && !HustleRules.Open(new Gig { Stage = GigStage.Booked, From = 20, Until = 24 }, 24) && !HustleRules.Open(new Gig { Stage = GigStage.Offered, From = 20, Until = 24 }, 21),
            "hustles: a booked visit may begin an hour early and until the client stops waiting; one not booked cannot");
        var ratings = HustleFresh(1);
        int count = ratings.Ratings.Count;
        var test = new Gig { Id = 7, Kind = GigKind.Handyman, Pay = 130, Stage = GigStage.Booked, Test = true };
        HustleRules.Worked(ratings, test, 90, true, 50);
        var tc = new Gig { Id = 8, Kind = GigKind.Handyman, Pay = 130, Stage = GigStage.Booked, Test = true };
        HustleRules.Cancel(ratings, tc);
        Check(ratings.Ratings.Count == count && ratings.JobsDone == 0 && ratings.Earned == 0 && test.Rating >= 1 && tc.Stage == GigStage.Cancelled,
            "hustles: a test's visit leaves the player's rating, visits and earnings as they were");
        var c = HustleFresh(1);
        var booked = new Gig { Id = 9, Stage = GigStage.Booked };
        HustleRules.Cancel(c, booked);
        Check(booked.Stage == GigStage.Cancelled && c.Ratings.Count == 1 && c.Ratings[0] == HustleCatalogue.CancelRating, "hustles: a visit called off leaves two stars");
        var r = HustleFresh(1);
        for (int i = 0; i < 30; i++)
        {
            r.Rate(i < 10 ? 1 : 5);
        }
        Check(r.Ratings.Count == HustleCatalogue.RatingsKept && r.Rating == 5000 && HustleFresh(1).Rating == 0, "hustles: the rating is the average of the last twenty");
        Check(HustleRules.GigPay(HustleCatalogue.Gig(GigKind.Junk), 1000, 0, 1000) == 220 && HustleRules.GigPay(HustleCatalogue.Gig(GigKind.Junk), 1000, 4600, 1000) == 240
              && HustleRules.GigPay(HustleCatalogue.Gig(GigKind.Junk), 2000, 0, 1000) == 440, "hustles: a visit pays the table's at the price level, a tenth more for a rating of 4.5 or better");
        Check(HustleRules.Hours(HustleCatalogue.Gig(GigKind.Moving)) == 3 && HustleRules.Hours(HustleCatalogue.Gig(GigKind.Handyman)) == 2, "hustles: a helper's hours are the visit's, both stops of a move");
    }

    // ---------------------------------------------------------------- the save

    private static void HustleStoreChecks()
    {
        var s = HustleFresh(123456);
        HustleRules.Morning(s, 20, HustleHomes(12), HustleCars, HustleColours, 1000);
        s.RepairXp = 33;
        s.Earned = 410;
        s.Profit = -25;
        s.JobsDone = 4;
        s.Rate(4);
        s.Rate(5);
        s.Items.Add(new Owned
        {
            Id = s.Next(), Thing = "laptop", Condition = 42, Paid = 300, Spent = 40, State = OwnedState.Kept, Plan = OwnedPlan.Shop, Street = "Shop | Street; x^", Number = 9,
            Seller = "Ann", Due = 21, Asking = 0, ListedDay = -1, Cleaned = true, Bought = 20, Test = true
        });
        s.Items.Add(new Owned
        {
            Id = s.Next(), Car = true, Thing = "ba:vehicletype_bima320", Colour = "Red", Km = 88000, Price = 28000, Condition = 66, Paid = 9000, State = OwnedState.Listed, Asking = 11000,
            ListedDay = 20, VehicleId = "abc+/=", Seller = "Bo", Street = "1st Avenue", Number = 3
        });
        s.Offers.Add(new BuyerOffer { Id = s.Next(), Item = s.Items[1].Id, Buyer = "Cy", Offer = 9500, Ceiling = 10500, Counters = 1, Expires = 500 });
        s.Sold(new Sale { Thing = "boots", Buyer = "Di", Price = 120, Cost = 80, Day = 19 });
        s.Gigs[0].Stage = GigStage.Helper;
        s.Gigs[0].HelperFee = 70;
        s.Gigs[0].HelperDone = 490;
        s.Gigs[0].HelperRating = 4;
        s.Gigs[1].Late = true;
        string line = HustleStore.Write(s);
        HustleState back = HustleStore.Read(line, out int version);
        Check(back != null && version == HustleStore.Version && HustleStore.Write(back) == line, "hustles: the save's line reads back to the same line");
        bool same = back != null && back.Seed == 123456 && back.Day == 20 && back.RepairXp == 33 && back.Earned == 410 && back.Profit == -25 && back.JobsDone == 4 && back.Ratings.Count == 2
                    && back.Items.Count == 2 && back.Items[0].Street == "Shop   Street  x " && back.Items[0].Plan == OwnedPlan.Shop && back.Items[0].Cleaned && back.Items[0].Test
                    && back.Items[1].VehicleId == "abc+/=" && back.Items[1].Car && back.Items[1].Km == 88000 && back.Items[1].Colour == "Red" && back.Items[1].State == OwnedState.Listed
                    && back.Offers.Count == 1 && back.Offers[0].Ceiling == 10500 && back.Sales.Count == 1 && back.Sales[0].Buyer == "Di"
                    && back.Gigs.Count == s.Gigs.Count && back.Gigs[0].Stage == GigStage.Helper && back.Gigs[0].HelperDone == 490 && back.Gigs[1].Late
                    && back.Listings.Count == s.Listings.Count && back.Listings[0].Seller == s.Listings[0].Seller;
        for (int i = 0; back != null && i < s.Gigs.Count; i++)
        {
            same &= back.Gigs[i].Street2 == s.Gigs[i].Street2 && back.Gigs[i].Number2 == s.Gigs[i].Number2 && back.Gigs[i].Client == s.Gigs[i].Client;
        }
        Check(same, "hustles: every field of the save's line comes back as written, the line's own marks in a text made spaces");
        Check(HustleStore.Read("v=1^q:1|2", out _) == null && HustleStore.Read("not mine", out _) == null && HustleStore.Read(string.Empty, out _) == null,
            "hustles: a line with no header, or not this feature's, is not read (and is kept as it was)");
        HustleState later = HustleStore.Read(line.Replace("v=1^", "v=2^") + "^z:9|9", out int v2);
        Check(later != null && v2 == 2 && later.Items.Count == 2, "hustles: a later version's line, with a part this one does not know, is read as far as it goes");
        HustleState shortLine = HustleStore.Read("v=1^h:5|3", out _);
        Check(shortLine != null && shortLine.Seed == 5 && shortLine.Day == 3 && shortLine.LastHour == -1 && shortLine.NextId == 1, "hustles: a short line reads with the defaults for what it lacks");
        Check(HustleRules.SeedOf("save one") == HustleRules.SeedOf("save one") && HustleRules.SeedOf("save one") != HustleRules.SeedOf("save two") && HustleRules.SeedOf(null) > 0,
            "hustles: the seed is the save's own name for the playthrough, the same on every machine");
    }

    // ---------------------------------------------------------------- a year of it

    private static void HustleBalanceChecks()
    {
        // A player who buys every listing at the seller's least, mends and cleans what can be, and sells at its worth.
        List<HustlePlace> homes = HustleHomes(30);
        long spent = 0, got = 0;
        int flips = 0;
        for (int day = 1; day <= 120; day++)
        {
            HustleState s = HustleFresh(99);
            s.RepairXp = 40;
            HustleRules.Morning(s, day, homes, HustleCars, HustleColours, 1000);
            foreach (Listing l in s.Listings.ToArray())
            {
                if (l.Car)
                {
                    continue;
                }
                Owned o = HustleRules.Buy(s, l, l.Floor, false, 0, day);
                ThingDef def = HustleCatalogue.Thing(o.Thing);
                for (int attempt = 0; attempt < 3 && HustleRules.CanFix(o); attempt++)
                {
                    o.Spent += HustleRules.PartsCost(def, 1000);
                    HustleRules.FixByHand(s, o, attempt);
                }
                if (HustleRules.CanClean(o))
                {
                    o.Spent += HustleCatalogue.CleanSupplies;
                    HustleRules.Clean(o);
                }
                spent += o.Paid + o.Spent;
                got += HustleRules.GoodsWorth(def, o.Condition, 1000);
                flips++;
            }
        }
        long margin = (got - spent) * 1000 / Math.Max(1, spent);
        Check(margin >= 150 && margin <= 900, "hustles: flipping played well makes 15 to 90 % on the money put in (" + margin / 10 + " % over " + flips + " things)");
        // A player who buys at the asking price and sells as bought makes far less, or loses.
        long rawSpent = 0, rawGot = 0;
        for (int day = 1; day <= 120; day++)
        {
            HustleState s = HustleFresh(99);
            HustleRules.Morning(s, day, homes, HustleCars, HustleColours, 1000);
            foreach (Listing l in s.Listings)
            {
                if (!l.Car)
                {
                    rawSpent += l.Asking;
                    rawGot += HustleRules.GoodsWorth(HustleCatalogue.Thing(l.Thing), l.Condition, 1000);
                }
            }
        }
        Check(rawGot * 1000 / Math.Max(1, rawSpent) < 1000, "hustles: buying at the asking price and selling as bought loses money (" + rawGot * 100 / Math.Max(1, rawSpent) + " % back)");
        // Visits: an hour of the player's work, at middling skill on time, against the jobs' pay.
        long pay = 0, minutes = 0;
        for (int i = 0; i < 400; i++)
        {
            var s = HustleFresh(7);
            GigDef def = HustleCatalogue.Gigs[i % 4];
            var g = new Gig { Id = i, Kind = def.Kind, Pay = HustleRules.GigPay(def, 1000, 0, 900 + i % 250), Stage = GigStage.Booked, Street2 = def.TwoStops ? "B" : null };
            int earned = HustleRules.Worked(s, g, def.Minutes, true, 50);
            if (def.TwoStops)
            {
                earned += HustleRules.Worked(s, g, def.Minutes, true, 50);
            }
            pay += earned;
            minutes += def.Minutes * (def.TwoStops ? 2 : 1);
        }
        long hourly = pay * 60 / Math.Max(1, minutes);
        Check(hourly >= 75 && hourly <= 125, "hustles: a freelance visit pays " + hourly + " an hour of work: above a gig on foot (40 to 55), near a gig with a car (100)");
    }

    private static void HustleTiming()
    {
        List<HustlePlace> homes = HustleHomes(300);
        var s = HustleFresh(5);
        var worths = new List<int>();
        var events = new List<HustleEvent>();
        for (int i = 0; i < 20; i++)
        {
            s.Items.Add(new Owned { Id = s.Next(), Thing = "phone", State = OwnedState.Listed, Asking = 300, Condition = 50 });
            worths.Add(300);
        }
        var watch = Stopwatch.StartNew();
        int runs = 0;
        for (int day = 1; day <= 50; day++)
        {
            HustleRules.Morning(s, day, homes, HustleCars, HustleColours, 1000);
            for (int h = 0; h < 24; h++)
            {
                events.Clear();
                HustleRules.Hour(s, day * 24 + h, worths, events);
                s.Offers.Clear();
                runs++;
            }
        }
        watch.Stop();
        double each = watch.Elapsed.TotalMilliseconds / runs;
        Check(each < 0.2, "hustles: an hour of the Marketplace, mornings included, takes " + each.ToString("0.000") + " ms (under 0.2 ms)");
    }

    // ---------------------------------------------------------------- the source

    private static string HustleScripts()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts", "Hustles"));
    }

    private static void HustleSource()
    {
        string folder = HustleScripts();
        bool free = true;
        foreach (string name in new[] { "HustleCatalogue.cs", "HustleState.cs", "HustleStore.cs", "HustleRules.cs" })
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, name));
            free &= !text.Contains("using UnityEngine") && !text.Contains("SaveGameManager") && !text.Contains("Random.") && !text.Contains("DateTime");
        }
        Check(free, "hustles: the catalogue, the state, the store and the rules are game-free and draw no unseeded chance");
        string director = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "HustleDirector.cs"));
        Check(director.Contains("if (now < _next)") && director.Contains("using var hotSpot = HotSpots.Time(HotUpdate);") && director.Contains("Guard.Fail(FrameGuard, e, this)"),
            "hustles: the director's frame is a timer comparison, timed and guarded");
        int touching = 0;
        foreach (string file in System.IO.Directory.GetFiles(folder, "*.cs"))
        {
            string text = System.IO.File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            if (name != "HustleGame.cs" && (text.Contains("ChangeMoneySafe") || text.Contains("CreateAndSpawnVehicle") || text.Contains("cityBuildingControllers")))
            {
                touching++;
            }
        }
        Check(touching == 0, "hustles: only HustleGame moves money, makes vehicles and walks the city's buildings");
        string places = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "HustleDirector.Places.cs"));
        Check(places.Contains("ShiftSkip.Offer(this, work)") && places.Contains("_door.Press(First)"), "hustles: work is begun at its place on the game's panel and done on the game's work panel");
    }

    private static void HustleTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("hustles", words, codes))
        {
            return;
        }
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var files = new List<string>(System.IO.Directory.GetFiles(HustleScripts(), "*.cs"));
        files.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(HustleScripts(), "..", "Wiring", "Wiring.HustleLinks.cs")));
        foreach (string file in files)
        {
            foreach (Match m in Regex.Matches(System.IO.File.ReadAllText(file), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string key in new[] { "reallife_opt_tip_hustles", "reallife_opt_sec_hustles" })
        {
            keys.Add(key);
        }
        foreach (string key in new List<string>(keys))
        {
            if (key.StartsWith("reallife_transaction_hustle_", StringComparison.Ordinal))
            {
                keys.Add(key + "_label");
            }
        }
        var lacking = new List<string>();
        foreach (string key in keys)
        {
            string missing = LanguagesLacking(words, codes, key);
            if (missing.Length > 0)
            {
                lacking.Add(key + " (" + missing + ")");
            }
        }
        Check(keys.Count > 250 && lacking.Count == 0, "hustles: each of its " + keys.Count + " texts is in every language with the English's names in curly brackets ("
                                                     + (lacking.Count == 0 ? "all" : string.Join(", ", lacking)) + ")");
        Dictionary<string, string> en = words[0];
        bool plain = true, short240 = true;
        foreach (string key in keys)
        {
            if (!en.TryGetValue(key, out string text) || !key.StartsWith("reallife_hustle", StringComparison.Ordinal) && !key.StartsWith("reallife_guide_hustles", StringComparison.Ordinal))
            {
                continue;
            }
            plain &= !(text.Contains("—") || text.Contains("–") || text.Contains(" - ") || text.Contains("!"));
            short240 &= !(key.StartsWith("reallife_hustle_note_", StringComparison.Ordinal) || key.EndsWith("_done", StringComparison.Ordinal)) || text.Length <= 240;
        }
        Check(plain, "hustles: its English has no dashes and no exclamation marks");
        Check(short240, "hustles: its notifications are at most 240 characters");
        string guide = en["reallife_guide_hustles"];
        bool facts = true;
        foreach (string fact in new[] { "{hustle_goods}", "{hustle_cars}", "{hustle_haggles}", "{hustle_shop}", "{hustle_window}", "{hustle_helper}", "{hustle_offer_hours}" })
        {
            facts &= guide.Contains(fact);
        }
        Check(facts, "hustles: the Guide's page fills its figures from the code");
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            Check(AnyChangesRow(words[l], l == 0 ? "A Marketplace app on the phone" : l == 1 ? "Um app Mercado" : "二手市场应用"), "hustles: What's new says so in " + codes[l]);
        }
    }
}
