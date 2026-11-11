using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Loyalty;

// Loyalty cards for the player's shops (run with --loyalty; Scripts/Loyalty): the tables and their bounds, members growing from the customers really served by how
// pleased they are, members falling away from a shop that is shut, dirty or dear and from a scheme that ended, the lift bounded on every day and exact over ten, the
// part it takes in the one figure of a business's footfall beside the interiors' and the others', what a day costs and when, the rule that a scheme never gives back
// more than the margin it brings unless the player chose a rate the page marks as a loss, the presses that cannot be played against the day's turn (the switch, the
// rate, the members' day), the other owners' shops by the same rules and what they take, the save (a round trip, an older line, a newer one, a damaged one, a decimal
// comma), the sources and the texts.
internal static partial class Program
{
    private static readonly string[] LoyaltyLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static void Loyalty()
    {
        LoyaltyNumberChecks();
        LoyaltyGrowthChecks();
        LoyaltyDecayChecks();
        LoyaltyLiftChecks();
        LoyaltyCostChecks();
        LoyaltyExploitChecks();
        LoyaltyTurnChecks();
        LoyaltyRivalChecks();
        LoyaltyStoreChecks();
        LoyaltySourceChecks();
        LoyaltyTextChecks();
    }

    private static LoyaltyFile LoyShop(int rate = 1, int push = 0, int offer = LoyaltyFile.NoDay)
    {
        return new LoyaltyFile { Place = "Main St#1", On = true, Want = true, Rate = rate, NextRate = rate, Push = push, OfferDay = offer, NextOffer = offer, OfferAsked = offer, Since = 1 };
    }

    // One day of a shop that draws 'customers' a day before the scheme's lift, each buying two products for 'ticket' cents in all, kept at 'margin' per mille; then the
    // next morning's turn. The lift written for the day is the lift the figure carried, as with no other part near its bounds.
    private static LoyaltyResult LoyDay(LoyaltyFile f, int day, int customers, int ticket, int margin, int satisfaction = 80, int pricing = 100, int clean = 100, int open = 10, int bad = 0)
    {
        int served = customers * (100 + f.Lift) / 100;
        int added = served * f.Basket / 1000;
        int items = served * 2 + added;
        long takings = (long)items * ticket / 2;
        f.Carried = f.Lift;
        f.Added = added;
        f.OpenHours = open;
        f.BadHours = bad;
        var d = new LoyaltyInput
        {
            Day = day, Weekday = LoyaltyRules.Weekday(day), Served = served, Items = items, Takings = takings, Goods = takings * (1000 - margin) / 1000,
            Satisfaction = satisfaction, Pricing = pricing, Clean = clean
        };
        LoyaltyResult r = LoyaltyRules.Day(f, d, PriceRules.Start);
        LoyaltyRules.Turn(f, day + 1);
        return r;
    }

    private static void LoyRun(LoyaltyFile f, int from, int days, int customers = 300, int ticket = 2000, int margin = 400, int satisfaction = 80)
    {
        for (int d = from; d < from + days; d++)
        {
            LoyDay(f, d, customers, ticket, margin, satisfaction);
        }
    }

    private static void LoyaltyNumberChecks()
    {
        Check(LoyaltyRules.RateMille.Length == LoyaltyRules.Rates && LoyaltyRules.RateAppeal.Length == LoyaltyRules.Rates && LoyaltyRules.RateVisits.Length == LoyaltyRules.Rates
              && LoyaltyRules.RateBasket.Length == LoyaltyRules.Rates && LoyaltyRules.RateKeeps.Length == LoyaltyRules.Rates && LoyaltyRules.PushJoin.Length == LoyaltyRules.Pushes
              && LoyaltyRules.PushCost.Length == LoyaltyRules.Pushes, "loyalty numbers: every table has a figure for each step");
        bool rising = true, less = true;
        for (int i = 1; i < LoyaltyRules.Rates; i++)
        {
            int step = LoyaltyRules.RateMille[i] - LoyaltyRules.RateMille[i - 1];
            rising &= step > 0 && LoyaltyRules.RateAppeal[i] > LoyaltyRules.RateAppeal[i - 1] && LoyaltyRules.RateVisits[i] > LoyaltyRules.RateVisits[i - 1]
                      && LoyaltyRules.RateBasket[i] > LoyaltyRules.RateBasket[i - 1] && LoyaltyRules.RateKeeps[i] < LoyaltyRules.RateKeeps[i - 1];
            if (i > 1)
            {
                int before = LoyaltyRules.RateMille[i - 1] - LoyaltyRules.RateMille[i - 2];
                // what a point of rate buys falls with every step: the gain of this step over its points, against the last step's
                less &= (long)(LoyaltyRules.RateVisits[i] - LoyaltyRules.RateVisits[i - 1]) * before < (long)(LoyaltyRules.RateVisits[i - 1] - LoyaltyRules.RateVisits[i - 2]) * step
                        && (long)(LoyaltyRules.RateAppeal[i] - LoyaltyRules.RateAppeal[i - 1]) * before < (long)(LoyaltyRules.RateAppeal[i - 1] - LoyaltyRules.RateAppeal[i - 2]) * step;
            }
        }
        Check(rising, "loyalty numbers: a higher reward rate gives more back, draws more members and visits, fills baskets more and keeps members better");
        Check(less, "loyalty numbers: each step of the reward rate buys fewer visits and sign-ups a point than the step before (diminishing returns)");
        Check(LoyaltyRules.PushJoin[0] == 1000 && LoyaltyRules.PushCost[0] == 0 && LoyaltyRules.PushJoin[2] > LoyaltyRules.PushJoin[1] && LoyaltyRules.PushCost[2] > LoyaltyRules.PushCost[1]
              && LoyaltyRules.PushCost[1] > 0, "loyalty numbers: cards by the till cost nothing and each stronger push costs more for each new member");
        Check((long)LoyaltyRules.PoolMost * LoyaltyRules.RateVisits[LoyaltyRules.Rates - 1] / 1000 <= 80
              && (long)LoyaltyRules.PoolMost * LoyaltyRules.RateVisits[LoyaltyRules.Rates - 1] / 1000 * LoyaltyRules.OfferVisits / 1000 <= LoyaltyRules.LiftMost * 10,
            "loyalty numbers: the top rate with a full card base adds 8% of customers at most on an ordinary day, and no more than the bound on a members' day");
        Check(LoyaltyRules.LiftMost <= BusinessCatalogue.AmbientMostBoost, "loyalty numbers: a scheme's whole lift fits inside the bound business pressure keeps on the part it is written in");
        Check(LoyaltyRules.RateMille[LoyaltyRules.Rates - 1] * LoyaltyRules.OfferPoints <= LoyaltyRules.RewardMost, "loyalty numbers: the top rate on a members' day is within the most a day's rewards may be");
        Check((LoyaltyRules.OfferVisits + (LoyaltyRules.WeekDays - 1) * LoyaltyRules.OfferOther) / LoyaltyRules.WeekDays <= 1010
              && (LoyaltyRules.OfferVisits + (LoyaltyRules.WeekDays - 1) * LoyaltyRules.OfferOther) / LoyaltyRules.WeekDays >= 990,
            "loyalty numbers: a members' day moves the week's visits to one day and makes none");
        Check(LoyaltyRules.Weekday(1) == 0 && LoyaltyRules.Weekday(7) == 6 && LoyaltyRules.Weekday(8) == 0 && LoyaltyRules.Weekday(0) == 6, "loyalty numbers: day 1 is a Monday, as the game counts");
        Check(LoyaltyRules.SetupCost(0) == LoyaltyRules.SetupFee && LoyaltyRules.SetupCost(2) == LoyaltyRules.SetupFee * LoyaltyRules.ChainSetupShare / 1000
              && LoyaltyRules.SetupCost(2) < LoyaltyRules.SetupCost(0) && LoyaltyRules.ChangeCost() == LoyaltyRules.SetupFee * LoyaltyRules.ChangeShare / 1000 && LoyaltyRules.ChangeCost() > 0,
            "loyalty numbers: a shop joining its brand's scheme pays a share of the set-up, and a new rate costs its printing");
    }

    private static void LoyaltyGrowthChecks()
    {
        LoyaltyFile f = LoyShop();
        bool grew = true, capped = true;
        int last = 0;
        for (int d = 1; d <= 12; d++)
        {
            LoyaltyResult r = LoyDay(f, d, 300, 2000, 400);
            grew &= f.Members > last && r.Signups > 0;
            capped &= f.Members <= LoyaltyRules.Cap(f.Base, 0);
            last = f.Members;
        }
        Check(grew, "loyalty growth: members build up day by day from the customers served");
        LoyRun(f, 13, 500);
        capped &= f.Members <= LoyaltyRules.Cap(f.Base, 0);
        Check(capped && LoyaltyRules.Share(f.Members, f.Base) <= LoyaltyRules.PoolMost, "loyalty growth: members never pass the share of the customer base that will carry a card");
        int settled = f.Members;
        LoyRun(f, 513, 70);
        Check(Math.Abs(f.Members - settled) <= settled / 20 + 100, "loyalty growth: a scheme settles: after 500 days ten more weeks move its members by a twentieth at most (" + settled + " -> " + f.Members + ")");

        LoyaltyFile pleased = LoyShop(), middling = LoyShop(), sour = LoyShop();
        LoyRun(pleased, 1, 40, satisfaction: 85);
        LoyRun(middling, 1, 40, satisfaction: 60);
        LoyRun(sour, 1, 40, satisfaction: LoyaltyRules.PleasedFrom);
        Check(pleased.Members > middling.Members && middling.Members > 0 && sour.Members == 0, "loyalty growth: pleased customers take the card sooner, and at the least satisfaction nobody does ("
            + pleased.Members + ", " + middling.Members + ", " + sour.Members + ")");
        Check(LoyaltyRules.Pleased(LoyaltyRules.PleasedFrom) == 0 && LoyaltyRules.Pleased(LoyaltyRules.PleasedFull) == 1000 && LoyaltyRules.Pleased(100) == 1000 && LoyaltyRules.Pleased(0) == 0
              && LoyaltyRules.Pleased(60) == 500, "loyalty growth: satisfaction counts from its floor to its full mark and no further");

        LoyaltyFile none = LoyShop(), nobody = LoyShop();
        LoyRun(nobody, 1, 30, customers: 0);
        Check(nobody.Members == 0, "loyalty growth: a shop that serves nobody gains no member");
        LoyRun(none, 1, 30);
        LoyaltyFile staff = LoyShop(push: 1), bonus = LoyShop(push: 2);
        long staffCost = 0, noneCost = 0;
        for (int d = 1; d <= 30; d++)
        {
            staffCost += LoyDay(staff, d, 300, 2000, 400).Costs;
        }
        LoyRun(bonus, 1, 30);
        noneCost = 30L * LoyaltyRules.CardDay * 100;
        Check(bonus.Members > staff.Members && staff.Members > none.Members && staffCost > noneCost, "loyalty growth: a sign-up push brings more members sooner and costs for each of them");

        int low = LoyRunMembers(0, 60), middle = LoyRunMembers(1, 60), high = LoyRunMembers(3, 60);
        Check(high > middle && middle > low && low > 0, "loyalty growth: a higher reward rate draws more members (" + low + ", " + middle + ", " + high + ")");

        LoyaltyFile alone = LoyShop(rate: 3, push: 2), chain = LoyShop(rate: 3, push: 2);
        chain.Sisters = 3;
        LoyRun(alone, 1, 400, satisfaction: 100);
        LoyRun(chain, 1, 400, satisfaction: 100);
        Check(LoyaltyRules.Cap(10000, 3) > LoyaltyRules.Cap(10000, 0) && LoyaltyRules.Cap(10000, 50) == LoyaltyRules.Cap(10000, LoyaltyRules.ChainReachMost / LoyaltyRules.ChainReach)
              && chain.Members >= alone.Members, "loyalty growth: a brand's other shops taking the card raise the most members a shop can have, up to a bound");

        LoyaltyFile twin = LoyShop();
        LoyRun(twin, 1, 30);
        Check(twin.Members == none.Members && twin.Base == none.Base && twin.Lift == none.Lift && twin.Basket == none.Basket, "loyalty growth: the same days give the same members, lift and basket on every machine");
    }

    private static int LoyRunMembers(int rate, int days)
    {
        LoyaltyFile f = LoyShop(rate: rate);
        LoyRun(f, 1, days);
        return f.Members;
    }

    private static void LoyaltyDecayChecks()
    {
        int everyday = LoyaltyRules.Lapse(1, false, 10, 0, 100, 100);
        Check(everyday == LoyaltyRules.LapseBase, "loyalty decay: a well run shop loses the everyday share of its members and no more");
        Check(LoyaltyRules.Lapse(1, false, 10, 10, 100, 100) == everyday + LoyaltyRules.LapseShutMost && LoyaltyRules.Lapse(1, false, 10, 5, 100, 100) == everyday + LoyaltyRules.LapseShutMost / 2
              && LoyaltyRules.Lapse(1, false, 0, 0, 100, 100) == everyday && LoyaltyRules.Lapse(1, false, 10, 40, 100, 100) == everyday + LoyaltyRules.LapseShutMost,
            "loyalty decay: every opening hour that could serve nobody (closed by its switch, empty shelves, nobody at the till) drives members away, a day of rest does not");
        Check(LoyaltyRules.Lapse(1, false, 10, 0, 0, 100) == everyday + LoyaltyRules.LapseDirtyMost && LoyaltyRules.Lapse(1, false, 10, 0, LoyaltyRules.CleanFrom, 100) == everyday
              && LoyaltyRules.Lapse(1, false, 10, 0, LoyaltyRules.CleanFrom / 2, 100) > everyday, "loyalty decay: a dirty shop loses members faster, a clean one does not");
        Check(LoyaltyRules.Lapse(1, false, 10, 0, 100, 0) == everyday + LoyaltyRules.LapseDearMost && LoyaltyRules.Lapse(1, false, 10, 0, 100, LoyaltyRules.PricesFrom) == everyday
              && LoyaltyRules.Lapse(1, false, 10, 0, 100, LoyaltyRules.PricesFrom / 2) > everyday, "loyalty decay: a shop whose prices are turned down loses members faster");
        Check(LoyaltyRules.Lapse(0, false, 10, 10, 0, 0) <= LoyaltyRules.LapseMost && LoyaltyRules.Lapse(0, false, 10, 10, 0, 0) > LoyaltyRules.Lapse(1, false, 10, 10, 100, 100),
            "loyalty decay: the worst day is bounded");
        Check(LoyaltyRules.Lapse(3, false, 10, 0, 100, 100) < everyday && LoyaltyRules.Lapse(0, false, 10, 0, 100, 100) > everyday && LoyaltyRules.Lapse(1, true, 10, 0, 100, 100) < everyday,
            "loyalty decay: a better rate and a members' day keep members better");

        LoyaltyFile well = LoyShop(), shut = LoyShop(), dirty = LoyShop(), dear = LoyShop(), ended = LoyShop();
        foreach (LoyaltyFile f in new[] { well, shut, dirty, dear, ended })
        {
            LoyRun(f, 1, 200);
        }
        int start = well.Members;
        for (int d = 201; d <= 214; d++)
        {
            LoyDay(well, d, 300, 2000, 400);
            LoyDay(shut, d, 0, 2000, 400, open: 10, bad: 10);
            LoyDay(dirty, d, 300, 2000, 400, clean: 10);
            LoyDay(dear, d, 300, 2000, 400, pricing: 20);
        }
        Check(shut.Members < start / 2 && dirty.Members < well.Members && dear.Members < well.Members && well.Members >= start * 9 / 10,
            "loyalty decay: two weeks shut, dirty or dear cost members a well run shop keeps (" + well.Members + ", shut " + shut.Members + ", dirty " + dirty.Members + ", dear " + dear.Members + ")");
        ended.Want = false;
        LoyaltyRules.Turn(ended, 201);
        Check(!ended.On && ended.Lift == 0 && ended.Basket == 0, "loyalty decay: a scheme that ended brings nobody from the next morning");
        int before = ended.Members;
        LoyaltyResult off = LoyDay(ended, 201, 300, 2000, 400);
        Check(ended.Members == before - before * LoyaltyRules.LapseOff / 1000 && off.Rewards == 0 && off.Costs == 0 && off.Visits == 0, "loyalty decay: after it ends its members fall away and it costs nothing");
        for (int d = 202; d <= 300; d++)
        {
            LoyDay(ended, d, 300, 2000, 400);
        }
        Check(ended.Members == 0 && LoyaltyRules.Spent(ended, 400) && !LoyaltyRules.Spent(well, 215), "loyalty decay: an ended scheme empties, and then the save need not keep it");
    }

    private static void LoyaltyLiftChecks()
    {
        bool bounded = true, exact = true;
        int most = 0, ordinary = 0;
        for (int rate = 0; rate < LoyaltyRules.Rates; rate++)
        {
            for (int offer = LoyaltyFile.NoDay; offer < LoyaltyRules.WeekDays; offer++)
            {
                foreach (int members in new[] { 0, 1, 500, 29999, 30000, 90000, 5000000 })
                {
                    foreach (int rivalry in new[] { 0, 300, 1500, 90000 })
                    {
                        int sum = 0, mille = 0;
                        for (int day = 1; day <= 70; day++)
                        {
                            int lift = LoyaltyRules.Lift(members, 10000, rate, offer, LoyaltyRules.Weekday(day), rivalry, day);
                            bounded &= lift >= 0 && lift <= LoyaltyRules.LiftMost;
                            if (members <= 30000)
                            {
                                most = Math.Max(most, lift);   // a base of 100 customers a day holds 300 members at most (hundredths: 30,000)
                                ordinary = offer < 0 ? Math.Max(ordinary, lift) : ordinary;
                            }
                            sum += lift;
                            mille += LoyaltyRules.LiftMille(members, 10000, rate, offer, LoyaltyRules.Weekday(day), rivalry);
                        }
                        // without a members' day every day asks the same: the whole per cents of seventy days are the tenths' sum exactly
                        exact &= offer >= 0 || members > 30000 || sum * 10 == mille;
                    }
                }
            }
        }
        Check(bounded, "loyalty lift: every day's lift is between nothing and " + LoyaltyRules.LiftMost + "% of customers, whatever the members, the rate, the day and the rivals");
        Check(most <= LoyaltyRules.LiftMost && most >= 8 && ordinary <= 8 && ordinary >= 7, "loyalty lift: the most is reached only on a members' day at the top rate (" + most + "), 8% at most on an ordinary day (" + ordinary + ")");
        Check(exact, "loyalty lift: the tenths of a per cent are given as whole per cents on that many days in ten, so the lift over time is the rule's exactly");
        Check(LoyaltyRules.Lift(0, 10000, 3, 0, 0, 0, 5) == 0 && LoyaltyRules.LiftMille(30000, 0, 3, -1, 0, 0) == 0, "loyalty lift: no members, no lift");
        Check(LoyaltyRules.LiftMille(30000, 10000, 1, -1, 0, 0) > LoyaltyRules.LiftMille(15000, 10000, 1, -1, 0, 0)
              && LoyaltyRules.LiftMille(30000, 10000, 1, -1, 0, 0) == LoyaltyRules.LiftMille(60000, 20000, 1, -1, 0, 0), "loyalty lift: the lift follows the members' share of the customer base, not the shop's size");
        LoyaltyFile f = LoyShop();
        f.Members = 30000;
        f.Base = 10000;
        LoyaltyRules.Today(f, 15);
        int on = f.Lift, basket = f.Basket;
        f.On = false;
        LoyaltyRules.Today(f, 15);
        Check(on > 0 && basket > 0 && basket <= LoyaltyRules.RateBasket[1] && f.Lift == 0 && f.Basket == 0, "loyalty lift: a running scheme lifts customers and baskets a little, one that is off neither");
        Check(LoyaltyRules.VisitShare(30000, 10000, 1, -1, 0) > LoyaltyRules.Share(30000, 10000) && LoyaltyRules.VisitShare(30000, 10000, 1, -1, 0) < 1000
              && LoyaltyRules.VisitShare(0, 10000, 1, -1, 0) == 0, "loyalty lift: members are a larger share of the visits than of the customers, since they come more often");

        // The one figure: the scheme's lift goes into the part the city's economy, the company, the brands, Flare and the rivals share, beside the interior's own part.
        float game = 1.0f;
        int own = 30;
        int alone = BusinessCatalogue.Written(own, 0, 0, 0, game), interior = BusinessCatalogue.Written(own, 0, 0, 10, game);
        int scheme = BusinessCatalogue.Written(own, 0, 6, 0, game), both = BusinessCatalogue.Written(own, 0, 6, 10, game), flare = BusinessCatalogue.Written(own, 0, 5 + 6, 10, game);
        Check(scheme > alone && both > interior && both > scheme && flare > both, "loyalty lift: the scheme's customers stack on the interior's and the other parts' in the one figure, none wiping another ("
            + alone + ", interior " + interior + ", scheme " + scheme + ", both " + both + ", with Flare " + flare + ")");
        Check(BusinessCatalogue.Written(own, 0, 6, -12, game) < scheme && BusinessCatalogue.Written(own, -12, 6, 0, game) < scheme, "loyalty lift: a shortfall or a trouble still takes its customers while a scheme runs");
        Check(BusinessCatalogue.ClampAmbient(BusinessCatalogue.AmbientMostBoost + 6) - BusinessCatalogue.ClampAmbient(BusinessCatalogue.AmbientMostBoost) == 0
              && BusinessCatalogue.ClampAmbient(4 + 6) - BusinessCatalogue.ClampAmbient(4) == 6 && BusinessCatalogue.ClampAmbient(12 + 6) - BusinessCatalogue.ClampAmbient(12) == 3,
            "loyalty lift: what the shared part's bound leaves of the lift is what the scheme is told it carried, so the page never counts customers it did not bring");
        Check(BusinessCatalogue.Wiped(false, 40, 44, 0, 6, 0) && !BusinessCatalogue.Wiped(true, 44, 44, 0, 6, 0), "loyalty lift: a figure the game put back over the scheme's is written again, as the other parts' are");
    }

    private static void LoyaltyCostChecks()
    {
        LoyaltyFile f = LoyShop(rate: 1);
        f.Members = 30000;
        f.Base = 10000;
        int share = LoyaltyRules.VisitShare(f.Members, f.Base, 1, -1, 0);
        var d = new LoyaltyInput { Day = 8, Weekday = 0, Served = 100, Items = 200, Takings = 400000, Goods = 240000, Satisfaction = 80, Pricing = 100, Clean = 100 };
        LoyaltyResult r = LoyaltyRules.Day(f, d, PriceRules.Start);
        Check(r.Rewards == 400000L * share / 1000 * 20 / 1000 && r.Visits == 100 * share / 1000, "loyalty cost: rewards are the rate's share of what members spent (" + r.Rewards + ")");
        Check(r.Costs == LoyaltyRules.CardDay * 100L, "loyalty cost: cards by the till cost the card's running fee a day and nothing for a new member");
        Check(f.Week[8 % LoyaltyRules.WeekDays].Day == 8 && f.Week[8 % LoyaltyRules.WeekDays].Rewards == r.Rewards && LoyaltyRules.Week(f, 9).Rewards == r.Rewards && LoyaltyRules.Week(f, 8).Rewards == 0
              && LoyaltyRules.Week(f, 16).Rewards == 0, "loyalty cost: a day's figures are kept for the page's week, counted from the day after and for seven days");

        LoyaltyFile offer = LoyShop(rate: 1, offer: 0);
        offer.Members = 30000;
        offer.Base = 10000;
        int offerShare = LoyaltyRules.VisitShare(offer.Members, offer.Base, 1, 0, 0);
        LoyaltyResult doubled = LoyaltyRules.Day(offer, d, PriceRules.Start);
        Check(doubled.Rewards == 400000L * offerShare / 1000 * 20 / 1000 * LoyaltyRules.OfferPoints && offerShare > share, "loyalty cost: on the members' day more of the customers are members and the rewards are doubled");

        LoyaltyFile push = LoyShop(rate: 1, push: 2);
        push.Base = 10000;
        LoyaltyResult joined = LoyaltyRules.Day(push, d, PriceRules.Start);
        Check(joined.Signups > 0 && joined.Costs == (long)joined.Signups * LoyaltyRules.PushCost[2] / 100 + LoyaltyRules.CardDay * 100L, "loyalty cost: a push is paid for each new member");
        LoyaltyFile dearer = LoyShop(rate: 1, push: 2);
        dearer.Base = 10000;
        LoyaltyResult later = LoyaltyRules.Day(dearer, d, 2000);
        Check(later.Costs == joined.Costs * 2 && later.Rewards == joined.Rewards, "loyalty cost: the card's costs follow the city's prices, the rewards follow the takings");

        LoyaltyFile idle = LoyShop(rate: 3, offer: 0);
        idle.Members = 30000;
        idle.Base = 10000;
        var nothing = new LoyaltyInput { Day = 8, Weekday = 0, Served = 0, Satisfaction = 80, Pricing = 100, Clean = 100 };
        Check(LoyaltyRules.Day(idle, nothing, PriceRules.Start).Rewards == 0, "loyalty cost: a day without takings gives no rewards");
        bool within = true;
        for (int rate = 0; rate < LoyaltyRules.Rates; rate++)
        {
            LoyaltyFile full = LoyShop(rate: rate, offer: 0);
            full.Members = 90000000;
            full.Base = 10000;
            within &= LoyaltyRules.Day(full, d, PriceRules.Start).Rewards <= d.Takings * LoyaltyRules.RewardMost / 1000;
        }
        Check(within, "loyalty cost: a day's rewards never pass the bound on a day's takings");
        Check(LoyaltyRules.Extra(100000, 0, 0, 200) == 0 && LoyaltyRules.Extra(110000, 10, 0, 200) == 10000 && LoyaltyRules.Extra(0, 10, 50, 200) == 0
              && LoyaltyRules.Extra(100000, 0, 20, 200) == 10000 && LoyaltyRules.Extra(100000, 0, 500, 200) == 50000 && LoyaltyRules.Extra(100000, 99, 0, 200) == LoyaltyRules.Extra(100000, LoyaltyRules.LiftMost, 0, 200),
            "loyalty cost: the extra takings are the share the lift really carried and the products put in baskets, and nothing without either");
    }

    private static void LoyaltyExploitChecks()
    {
        bool pays = true, loses = true, capped = true, sound = true;
        string worst = string.Empty;
        for (int rate = 0; rate < LoyaltyRules.Rates; rate++)
        {
            for (int offer = LoyaltyFile.NoDay; offer <= 0; offer++)
            {
                for (int push = 0; push < LoyaltyRules.Pushes; push++)
                {
                    int even = LoyaltyRules.BreakEven(rate, offer >= 0, 0);
                    foreach (int margin in new[] { even, even + 100, even / 2 })
                    {
                        LoyaltyFile f = LoyShop(rate, push, offer);
                        LoyRun(f, 1, 420, margin: margin);
                        long rewards = 0, brought = 0;
                        for (int d = 421; d <= 490; d++)
                        {
                            LoyaltyResult r = LoyDay(f, d, 300, 2000, margin);
                            rewards += r.Rewards;
                            brought += r.Margin;
                            capped &= r.Rewards <= (long)(300 * 112 / 100 * 2 + 300) * 1000 * LoyaltyRules.RewardMost / 1000 && r.Extra >= 0 && f.Lift <= LoyaltyRules.LiftMost;
                        }
                        if (margin >= even && brought < rewards)
                        {
                            pays = false;
                            worst = "rate " + rate + ", members' day " + offer + ", push " + push + ", margin " + margin + ": brought " + brought + ", gave back " + rewards;
                        }
                        loses &= margin >= even || brought < rewards;
                        sound &= LoyaltyRules.Sound(rate, offer >= 0, 0, margin) == (margin >= even);
                    }
                }
            }
        }
        Check(pays, "loyalty no exploit: at a margin the page calls enough, a settled scheme never gives back more than the margin it brings, at every rate, push and members' day" + (worst.Length > 0 ? " (" + worst + ")" : string.Empty));
        Check(loses, "loyalty no exploit: at half that margin it does give back more: the loss the page warns of is real, and the player's own choice");
        Check(sound && LoyaltyRules.Sound(3, true, 0, LoyaltyRules.MarginUnknown), "loyalty no exploit: the page's verdict is the break even's, and a shop with no books yet is not judged");
        Check(capped, "loyalty no exploit: every day's rewards and lift stay inside their bounds through every run");
        bool rises = true;
        for (int rate = 1; rate < LoyaltyRules.Rates; rate++)
        {
            rises &= LoyaltyRules.BreakEven(rate, false, 0) > LoyaltyRules.BreakEven(rate - 1, false, 0);
        }
        Check(rises && LoyaltyRules.BreakEven(1, true, 0) > LoyaltyRules.BreakEven(1, false, 0) && LoyaltyRules.BreakEven(1, false, 600) > LoyaltyRules.BreakEven(1, false, 0)
              && LoyaltyRules.BreakEven(0, false, 0) > 100 && LoyaltyRules.BreakEven(LoyaltyRules.Rates - 1, true, 1500) <= 1000,
            "loyalty no exploit: no setting is best everywhere: each higher rate, a members' day and rival cards all need a better margin (" + LoyaltyRules.BreakEven(0, false, 0) + ", "
            + LoyaltyRules.BreakEven(1, false, 0) + ", " + LoyaltyRules.BreakEven(2, false, 0) + ", " + LoyaltyRules.BreakEven(3, false, 0) + " per mille)");

        // Switching off and on around the day's turn: a day with a lift is always a day whose rewards are booked, and the members pay for every day off.
        LoyaltyFile steady = LoyShop(), flip = LoyShop();
        LoyRun(steady, 1, 200);
        LoyRun(flip, 1, 200);
        bool paid = true;
        for (int d = 201; d <= 260; d++)
        {
            LoyDay(steady, d, 300, 2000, 400);
            bool ran = flip.On && flip.Lift > 0;
            flip.Want = d % 2 == 0;
            LoyaltyResult r = LoyDay(flip, d, 300, 2000, 400);
            paid &= !ran || r.Rewards > 0;
            paid &= flip.On || flip.Lift == 0;
        }
        Check(paid && flip.Members < steady.Members / 2, "loyalty no exploit: a scheme switched off and on day by day pays the rewards of every day it lifted, and loses its members ("
            + flip.Members + " against " + steady.Members + ")");

        // The rate is the rate of the whole day: what is chosen comes in at the turn, and the rewards of a day are at the rate its lift was written with.
        LoyaltyFile hop = LoyShop(rate: 3);
        LoyRun(hop, 1, 200);
        int members = hop.Members;
        hop.NextRate = 0;
        Check(hop.Rate == 3, "loyalty no exploit: a rate chosen during the day is not in force before the next morning");
        LoyaltyResult top = LoyDay(hop, 201, 300, 2000, 400);
        LoyaltyFile same = LoyShop(rate: 3);
        LoyRun(same, 1, 200);
        LoyaltyResult kept = LoyDay(same, 201, 300, 2000, 400);
        Check(top.Rewards == kept.Rewards && hop.Rate == 0, "loyalty no exploit: the day's rewards are paid at the rate the day ran on, whatever was chosen meanwhile");
        Check(hop.Members < same.Members && same.Members - hop.Members >= (long)members * LoyaltyRules.CutLapse * 3 / 1000 * 9 / 10, "loyalty no exploit: a cut in the rate loses members at once, a share for each step");

        // The members' day needs its notice: one chosen anew every day never comes.
        LoyaltyFile daily = LoyShop();
        bool never = true;
        for (int d = 1; d <= 40; d++)
        {
            daily.NextOffer = LoyaltyRules.Next(daily.NextOffer, LoyaltyFile.NoDay, LoyaltyRules.WeekDays - 1);
            LoyDay(daily, d, 300, 2000, 400);
            never &= daily.OfferDay == LoyaltyFile.NoDay;
        }
        Check(never, "loyalty no exploit: a members' day chosen anew every day never comes into force");
        LoyaltyFile once = LoyShop();
        once.NextOffer = 2;
        int first = 0;
        for (int d = 1; d <= 20 && first == 0; d++)
        {
            LoyDay(once, d, 300, 2000, 400);
            first = once.OfferDay == 2 ? d + 1 : 0;
        }
        Check(first == 2 + LoyaltyRules.OfferNotice, "loyalty no exploit: a members' day comes into force " + LoyaltyRules.OfferNotice + " days after the morning it was announced (day " + first + ")");
        once.NextOffer = LoyaltyFile.NoDay;
        LoyaltyRules.Turn(once, first + 1);
        Check(once.OfferDay == LoyaltyFile.NoDay && once.OfferFrom == 0, "loyalty no exploit: a members' day is stopped without notice");
    }

    private static void LoyaltyTurnChecks()
    {
        var f = new LoyaltyFile { Place = "A#1", Want = true, NextRate = 2, LastDay = 4 };
        Check(!LoyaltyRules.Turn(f, 5) && f.On && f.Since == 5 && f.Rate == 2 && f.LastDay == 5 && f.Lift == 0, "loyalty turn: a scheme switched on runs from the next morning at the rate chosen, with nothing to print");
        f.NextRate = 3;
        Check(LoyaltyRules.Turn(f, 6) && f.Rate == 3, "loyalty turn: a new rate on a running scheme comes in at the turn and its printing is to be paid");
        Check(!LoyaltyRules.Turn(f, 7), "loyalty turn: nothing is printed while nothing changes");
        f.OpenHours = 9;
        f.BadHours = 4;
        f.Added = 12;
        f.Carried = 5;
        LoyaltyRules.Turn(f, 8);
        Check(f.OpenHours == 0 && f.BadHours == 0 && f.Added == 0 && f.Carried == 0, "loyalty turn: the hours' count, the baskets and what the figure carried start again each morning");
        f.Want = false;
        f.NextRate = 0;
        Check(!LoyaltyRules.Turn(f, 9) && !f.On && f.Rate == 0, "loyalty turn: a scheme that ends at the turn prints nothing");
        Check(LoyaltyRules.Next(0, 0, 3) == 1 && LoyaltyRules.Next(3, 0, 3) == 0 && LoyaltyRules.Next(-1, -1, 6) == 0 && LoyaltyRules.Next(6, -1, 6) == -1 && LoyaltyRules.Next(99, 0, 3) == 0,
            "loyalty turn: a choice goes round its steps");
        var wild = new LoyaltyFile { Rate = 9, NextRate = -4, Push = 7, OfferDay = 12, NextOffer = -9, Members = -5, Base = -1, Lift = 99, Basket = 5000, OpenHours = 30, BadHours = 40, Carried = 50, Paid = -3 };
        LoyaltyRules.Bound(wild);
        Check(wild.Rate == LoyaltyRules.Rates - 1 && wild.NextRate == 0 && wild.Push == LoyaltyRules.Pushes - 1 && wild.OfferDay == 6 && wild.NextOffer == LoyaltyFile.NoDay && wild.Members == 0
              && wild.Base == 0 && wild.Lift == LoyaltyRules.LiftMost && wild.Basket == 1000 && wild.OpenHours == 24 && wild.BadHours == 24 && wild.Carried == LoyaltyRules.LiftMost && wild.Paid == 0,
            "loyalty turn: every figure read from a save is put inside its bounds");
    }

    private static void LoyaltyRivalChecks()
    {
        var good = new LoyaltyRival { Seed = 0, Quality = 80, Days = 200, Tycoon = true };
        int tycoons = 0, city = 0, strongest = 0;
        bool bounded = true, same = true;
        for (int seed = 1; seed <= 2000; seed++)
        {
            var t = new LoyaltyRival { Seed = seed * 7919, Quality = 80, Days = 200, Tycoon = true };
            var c = new LoyaltyRival { Seed = seed * 7919, Quality = 80, Days = 200, Tycoon = false };
            tycoons += LoyaltyRules.RivalRuns(t) ? 1 : 0;
            city += LoyaltyRules.RivalRuns(c) ? 1 : 0;
            if (seed <= 200)
            {
                int strength = LoyaltyRules.RivalStrength(t);
                bounded &= strength >= 0 && strength <= LoyaltyRules.PoolMost && (strength > 0) == LoyaltyRules.RivalRuns(t) && LoyaltyRules.RivalRate(t) >= 0 && LoyaltyRules.RivalRate(t) < LoyaltyRules.Rates;
                same &= strength == LoyaltyRules.RivalStrength(t);
                strongest = Math.Max(strongest, strength);
                good = strength == strongest && strength > 0 ? t : good;
            }
        }
        Check(Math.Abs(tycoons - 2000 * LoyaltyRules.RivalKeenTycoon / 100) < 120 && Math.Abs(city - 2000 * LoyaltyRules.RivalKeenCity / 100) < 120 && tycoons > city,
            "loyalty rivals: about six in ten of the rivals' shops and a third of the city's would run a card (" + tycoons + " and " + city + " of 2000)");
        Check(bounded && same && strongest > 100, "loyalty rivals: a rival's scheme is worked out by the player's own rules, the same on every machine, and holds no more of its base than the player's can (" + strongest + ")");
        var poor = good;
        poor.Quality = LoyaltyRules.RivalFrom - 1;
        var young = good;
        young.Days = LoyaltyRules.RivalSetupDays - 1;
        var newer = good;
        newer.Days = LoyaltyRules.RivalSetupDays + 5;
        Check(LoyaltyRules.RivalStrength(poor) == 0 && LoyaltyRules.RivalStrength(young) == 0 && LoyaltyRules.RivalStrength(newer) > 0 && LoyaltyRules.RivalStrength(newer) < LoyaltyRules.RivalStrength(good),
            "loyalty rivals: a shop its customers do not like or one newly opened runs no card, and a card grows with its days");
        var list = new List<LoyaltyRival> { good, poor, young };
        int running, rivalry = LoyaltyRules.Rivalry(list, out running);
        Check(rivalry == LoyaltyRules.RivalStrength(good) && running == 1 && LoyaltyRules.Rivalry(null, out running) == 0 && running == 0, "loyalty rivals: a district's rival strength is the sum of the cards that run there");
        for (int i = 0; i < 20; i++)
        {
            list.Add(good);
        }
        Check(LoyaltyRules.Rivalry(list, out running) == LoyaltyRules.RivalryMost && running == 21, "loyalty rivals: the strength counted is bounded");
        Check(LoyaltyRules.RivalryKept(0) == 1000 && LoyaltyRules.RivalryKept(rivalry) < 1000 && LoyaltyRules.RivalryKept(1000) == 500 && LoyaltyRules.RivalryKept(LoyaltyRules.RivalryMost) >= LoyaltyRules.RivalFloor
              && LoyaltyRules.RivalryKept(int.MaxValue) == LoyaltyRules.RivalryKept(LoyaltyRules.RivalryMost), "loyalty rivals: every rival card leaves less of the lift, never under the floor");
        Check(LoyaltyRules.LiftMille(30000, 10000, 1, -1, 0, rivalry) < LoyaltyRules.LiftMille(30000, 10000, 1, -1, 0, 0) && LoyaltyRules.LiftMille(30000, 10000, 1, -1, 0, rivalry) > 0
              && LoyaltyRules.Join(80, 1, 0, false, rivalry) < LoyaltyRules.Join(80, 1, 0, false, 0), "loyalty rivals: a rival's scheme nearby lowers the lift and the sign-ups of the player's");
        LoyaltyFile free = LoyShop(), crowded = LoyShop();
        crowded.Rivalry = 1200;
        LoyRun(free, 1, 200);
        LoyRun(crowded, 1, 200);
        Check(crowded.Members < free.Members && crowded.Lift <= free.Lift, "loyalty rivals: in a district full of cards a scheme holds fewer members and lifts less (" + crowded.Members + " against " + free.Members + ")");
    }

    private static void LoyaltyStoreChecks()
    {
        var s = new LoyaltyState();
        LoyaltyFile a = LoyShop(2, 1, 4);
        a.Place = "Hudson St, 5|a;b^c";
        a.NextRate = 3;
        a.NextOffer = 6;
        a.OfferAsked = 6;
        a.OfferFrom = 77;
        a.Since = 12;
        a.Members = 123456;
        a.Base = 23456;
        a.LastDay = 70;
        a.Lift = 5;
        a.Basket = 31;
        a.OpenHours = 9;
        a.BadHours = 2;
        a.Added = 14;
        a.Carried = 4;
        a.Paid = 98765;
        a.Week[70 % 7] = new LoyaltyDay { Day = 70, Visits = 101, Signups = 250, Lapsed = 120, Rewards = 4455, Costs = 1200, Extra = 9900, Margin = 3960 };
        a.Week[69 % 7] = new LoyaltyDay { Day = 69, Visits = 99, Rewards = 4000, Costs = 1200 };
        s.Files.Add(a);
        s.Files.Add(new LoyaltyFile { Place = "Second" });
        string line = LoyaltyStore.Write(s);
        LoyaltyState back = LoyaltyStore.Read(line, out int version);
        LoyaltyFile b = back != null ? back.File(a.Place) : null;
        Check(version == LoyaltyStore.Version && back.Files.Count == 2 && b != null && b.On && b.Want && b.Rate == 2 && b.NextRate == 3 && b.Push == 1 && b.OfferDay == 4 && b.NextOffer == 6
              && b.OfferAsked == 6 && b.OfferFrom == 77 && b.Since == 12 && b.Members == 123456 && b.Base == 23456 && b.LastDay == 70 && b.Lift == 5 && b.Basket == 31 && b.OpenHours == 9
              && b.BadHours == 2 && b.Added == 14 && b.Carried == 4 && b.Paid == 98765, "loyalty save: a scheme comes back as it was written, whatever its place is called");
        Check(b != null && b.Week[0].Day == 70 && b.Week[0].Visits == 101 && b.Week[0].Signups == 250 && b.Week[0].Lapsed == 120 && b.Week[0].Rewards == 4455 && b.Week[0].Costs == 1200
              && b.Week[0].Extra == 9900 && b.Week[0].Margin == 3960 && b.Week[6].Day == 69 && b.Week[1].Day == 0, "loyalty save: the week's days come back, each in its place");
        Check(LoyaltyStore.Write(back) == line, "loyalty save: what was read writes the same line again");
        Check(LoyaltyStore.Key == "reallife:loyalty" && line.StartsWith("v=1^f:", StringComparison.Ordinal), "loyalty save: the line is under the mod's own key and begins with its version");
        LoyaltyFile second = back.File("Second");
        Check(second != null && !second.On && second.Rate == LoyaltyRules.RateStart && second.OfferDay == LoyaltyFile.NoDay && second.Members == 0, "loyalty save: a scheme never switched on keeps its starting terms");

        LoyaltyState newer = LoyaltyStore.Read(line.Replace("v=1", "v=7") + "|later|fields^z:something|new", out int later);
        Check(newer != null && later == 7 && newer.Files.Count == 2 && newer.File(a.Place) != null && newer.File(a.Place).Members == 123456, "loyalty save: a newer version's line is read as far as this one knows it");
        LoyaltyState older = LoyaltyStore.Read("v=1^f:Old%20St|1|1|2|2|0|-1|-1|-1|0|3|500|900|9", out int _);
        Check(older != null && older.Files.Count == 1 && older.Files[0].Place == "Old St" && older.Files[0].Members == 500 && older.Files[0].LastDay == 9 && older.Files[0].Lift == 0
              && older.Files[0].Paid == 0, "loyalty save: a record without the later fields still loads");
        Check(LoyaltyStore.Read("nonsense", out int _) == null && LoyaltyStore.Read(string.Empty, out int _) == null && LoyaltyStore.Read("v=x^f:a", out int _) == null, "loyalty save: a line that is not the feature's is not read");
        LoyaltyState damaged = LoyaltyStore.Read("v=1^f:A|1|1|x|y|z|q|q|q|q|q|q|q|q;|1|1;B|1^w:A|70|1|2;Nobody|70|1|2|3|4|5|6|7", out int _);
        Check(damaged != null && damaged.Files.Count == 1 && damaged.Files[0].Rate == LoyaltyRules.RateStart && damaged.Files[0].Members == 0 && damaged.Files[0].Week[0].Day == 0,
            "loyalty save: a damaged line gives what can be read, inside its bounds, and nothing else");
        var many = new LoyaltyState();
        for (int i = 0; i < LoyaltyRules.FilesMost + 20; i++)
        {
            many.Files.Add(new LoyaltyFile { Place = "P" + i, Want = true });
        }
        Check(LoyaltyStore.Read(LoyaltyStore.Write(many), out int _).Files.Count == LoyaltyRules.FilesMost, "loyalty save: no more schemes are read than a save keeps");
        CultureInfo was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            LoyaltyState german = LoyaltyStore.Read(LoyaltyStore.Write(s), out int _);
            Check(LoyaltyStore.Write(s) == line && german.File(a.Place).Paid == 98765, "loyalty save: the line is the same under a decimal comma");
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
        Check(LoyaltyStore.Write(new LoyaltyState()) == "v=1" && LoyaltyStore.Read("v=1", out int _).Files.Count == 0, "loyalty save: no scheme, an empty line");
    }

    private static void LoyaltySourceChecks()
    {
        string director = ModSource(@"Loyalty\LoyaltyDirector.cs"), day = ModSource(@"Loyalty\LoyaltyDirector.Day.cs"), game = ModSource(@"Loyalty\LoyaltyGame.cs");
        string rules = ModSource(@"Loyalty\LoyaltyRules.cs"), links = ModSource(@"Wiring\Wiring.LoyaltyLinks.cs"), wiring = ModSource(@"Wiring\Wiring.cs"), tab = ModSource(@"Loyalty\LoyaltyTab.cs");
        string pages = ModSource(@"Loyalty\LoyaltyTab.Pages.cs"), settings = ModSource(@"Core\Settings.cs");
        Check(director.Length > 0 && day.Length > 0 && game.Length > 0 && rules.Length > 0 && links.Length > 0 && tab.Length > 0 && pages.Length > 0, "loyalty sources: the feature's files are read");
        Check(!Regex.IsMatch(director + day, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate)\s*\(") && director.Contains("StartCoroutine(Guard.Routine(Tick()))")
              && director.Contains("EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen)") && director.Contains("EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen)"),
            "loyalty sources: nothing runs per frame: a second's look, the game's hour and the game's day");
        Check(!(director + day + game + rules + links).Contains("UnityEngine.Random") && !Regex.IsMatch(director + day + game + rules, @"\bRandom\.") && !rules.Contains("using UnityEngine")
              && !rules.Contains("DateTime"), "loyalty sources: nothing is drawn from the engine's dice or the machine's clock, so every machine works out the same schemes");
        Check(game.Contains("Economy.BizBooks.Book(-(float)amount, Info(key, reg), day, reg.Address, force)") && game.Contains("new TransactionInfo(key, Economy.GameBooks.Marketing,")
              && game.Contains("info.SetTaxDeductibleName(DeductKey.GetLocalization())") && !game.Contains("ChangeMoneySafe"),
            "loyalty sources: every cost goes through the mod's one booking call, at the business, as a marketing cost the tax deducts");
        Check(day.Contains("LoyaltyGame.Charge(reg, given, LoyaltyGame.RewardsKey, day - 1, true)") && day.Contains("LoyaltyGame.Charge(reg, upkeep, LoyaltyGame.CostsKey, day - 1, true)")
              && day.Contains("LoyaltyGame.Refresh(day - 1)"), "loyalty sources: the rewards and the card's costs are booked for the day they were given, and that day's statement is brought up to date");
        Check(director.Contains("return d != null && d.Running && reg != null && d._byReg.TryGetValue(reg, out LoyaltyFile f) && f.On ? f.Lift : 0;")
              && director.Contains("public bool Running => _loaded && !_broken && Settings.Loyalty;"), "loyalty sources: with the option off, or the scheme, nothing is added to a business's footfall");
        Check(links.Contains("_ambientBefore = BusinessGame.Ambient;") && links.Contains("BusinessGame.Ambient = Ambient;") && links.Contains("BusinessGame.Ambient = _ambientBefore;")
              && links.Contains("return before + lift;") && links.Contains("BusinessCatalogue.ClampAmbient(before + lift) - BusinessCatalogue.ClampAmbient(before)"),
            "loyalty sources: the lift goes on top of the earlier pairings' part of the one figure, and what it wrapped goes back as found");
        int connect = wiring.IndexOf("LoyaltyLinks.Connect", StringComparison.Ordinal), rivalsOn = wiring.IndexOf("RivalLinks.Connect", StringComparison.Ordinal);
        int standingOn = wiring.IndexOf("StandingLinks.Connect", StringComparison.Ordinal), disconnect = wiring.IndexOf("LoyaltyLinks.Disconnect", StringComparison.Ordinal);
        int rivalsOff = wiring.IndexOf("RivalLinks.Disconnect", StringComparison.Ordinal), standingOff = wiring.IndexOf("StandingLinks.Disconnect", StringComparison.Ordinal);
        Check(connect > rivalsOn && rivalsOn > 0 && connect < standingOn && disconnect > standingOff && standingOff > 0 && disconnect < rivalsOff,
            "loyalty sources: its pairing is joined after the rivals' and before Standing's, and taken off in the opposite order");
        Check(game.Contains("Quality = Crew(other),") && game.Contains("sum += other.aiEmployees[i].primarySkillValue;") && game.Contains("!other.aiEmployees[i].isPoached"),
            "loyalty sources: another owner's shop is judged by the skill of the crew the game holds for it, since the game keeps a satisfaction only for the player's");
        Check(game.Contains("other.RentedByPlayer") && game.Contains("GoingPublic.Foreign(other)") && links.Contains("!GoingPublic.InSession") && game.Contains("public static bool IsShop(BuildingRegistration reg) => BusinessGame.IsBusiness(reg);"),
            "loyalty sources: only the player's own businesses run a scheme, another player's shop is never read as a rival, and the tycoons rest in a shared city");
        Check(settings.Contains("public const string LoyaltyId = \"loyalty\";") && settings.Contains("public static bool Loyalty = true;") && settings.Contains("Loyalty = Bool(modId, LoyaltyId, Loyalty);"),
            "loyalty sources: the option is on by default and read with the others");
        Check(tab.Contains("using var timed = BizPaceWatch.Time(BizFix);") && pages.Contains("using var timed = BizPaceWatch.Time(BizWrite);") && pages.Contains("if (!Written())"),
            "loyalty sources: the tab's steps in BizMan are timed, and a page already written is not written again");
        Check(pages.IndexOf("reallife_loyalty_line_members", StringComparison.Ordinal) < pages.IndexOf("reallife_loyalty_head_today", StringComparison.Ordinal)
              && pages.IndexOf("reallife_loyalty_line_extra", StringComparison.Ordinal) < pages.IndexOf("reallife_loyalty_head_scheme", StringComparison.Ordinal)
              && pages.Contains("reallife_loyalty_line_visits") && pages.Contains("reallife_loyalty_line_rewards"), "loyalty sources: the page says the members, their visits, the rewards and the extra takings first");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("Loyalty.LoyaltyGuide.Register();") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"loyalty\").For(\"loyalty\").Opts(\"loyalty\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"loyalty\", CityScope.AnyCity,") && ModSource(@"Core\FeatureOwners.cs").Contains("{ \"RealLifeOverhaul.Loyalty\", \"loyalty\" }"),
            "loyalty sources: the feature is in the feature list, the options tree, the cities' list and the owners' list");
    }

    private static void LoyaltyTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string[] sources =
        {
            @"Loyalty\LoyaltyDirector.cs", @"Loyalty\LoyaltyDirector.Day.cs", @"Loyalty\LoyaltyDirector.Tests.cs", @"Loyalty\LoyaltyGame.cs", @"Loyalty\LoyaltyGuide.cs", @"Loyalty\LoyaltyTab.cs",
            @"Loyalty\LoyaltyTab.Pages.cs", @"Loyalty\LoyaltyText.cs"
        };
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(ModSource(source), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string k in new[] { "reallife_opt_sec_loyalty", "reallife_opt_tip_loyalty", "reallife_contract_part_loyalty_tab", "reallife_transaction_loyalty_rewards_label",
            "reallife_transaction_loyalty_costs_label", "reallife_transaction_loyalty_setup_label", "reallife_transaction_loyalty_change_label" })
        {
            keys.Add(k);
        }
        Check(keys.Count >= 90, "loyalty texts: its keys are found: " + keys.Count);
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(english != null, "loyalty texts: en.json is read");
        if (english == null)
        {
            return;
        }
        const string sentence = "Loyalty cards for your shops: start a scheme from the new Loyalty tab in BizMan";
        string row = english.Keys.FirstOrDefault(k => k.StartsWith("reallife_changes_", StringComparison.Ordinal) && !k.EndsWith("_title", StringComparison.Ordinal) && english[k].Contains(sentence));
        Check(row != null, "loyalty texts: What's new has the feature's line in English");
        if (row == "reallife_changes_next")
        {
            int at = english[row].IndexOf(sentence, StringComparison.Ordinal), mark = english[row].IndexOf("Fixed: ", StringComparison.Ordinal);
            Check(mark < 0 || at < mark, "loyalty texts: the feature's line stands before the first fix mark");
        }
        foreach (string key in keys)
        {
            bool there = english.TryGetValue(key, out string value) && value.Length > 0;
            Check(there, "loyalty texts: " + key + " is in en.json");
            if (!there)
            {
                continue;
            }
            Check(!value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - "), "loyalty texts: " + key + " has no exclamation mark or dash");
            if (key.Contains("_note_") || key.Contains("_refuse_") || key.Contains("_q_") || (key.Contains("_test_") && !key.EndsWith("_button", StringComparison.Ordinal)))
            {
                Check(value.Length <= 240, "loyalty texts: the notice " + key + " is at most 240 characters (" + value.Length + ")");
            }
        }
        // The other languages are filled by the translation step: where one has a key, it keeps the English's names, tags and lines.
        foreach (string code in LoyaltyLanguages)
        {
            Dictionary<string, string> words = code == "en" ? null : LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            foreach (string key in keys)
            {
                if (words == null || !words.TryGetValue(key, out string value) || !english.TryGetValue(key, out string en))
                {
                    continue;
                }
                var wanted = new SortedSet<string>(Regex.Matches(en, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                var got = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                Check(wanted.SetEquals(got), "loyalty texts: " + key + " keeps its {names} in " + code);
                Check(Regex.Matches(en, "<b>").Count == Regex.Matches(value, "<b>").Count && Regex.Matches(en, @"\n").Count == Regex.Matches(value, @"\n").Count, "loyalty texts: " + key + " keeps its tags and lines in " + code);
            }
        }
        string guideSource = ModSource(@"Loyalty\LoyaltyGuide.cs");
        if (english.TryGetValue("reallife_guide_loyalty", out string page))
        {
            var asked = new SortedSet<string>(Regex.Matches(page, @"\{([a-z_0-9]+)\}").Cast<Match>().Select(m => m.Groups[1].Value));
            var filled = new SortedSet<string>(Regex.Matches(guideSource, "\\.Fact\\(\"([a-z_0-9]+)\"").Cast<Match>().Select(m => m.Groups[1].Value));
            Check(asked.Count >= 30 && asked.IsSubsetOf(filled), "loyalty texts: every figure of the Guide page is filled from the rules (" + string.Join(", ", asked.Except(filled)) + ")");
            Check(filled.IsSubsetOf(asked), "loyalty texts: every fact the feature fills is said on its Guide page (" + string.Join(", ", filled.Except(asked)) + ")");
            Check(new[] { "reallife_loyalty_tab", "reallife_loyalty_enabled", "reallife_loyalty_btn_start", "reallife_loyalty_btn_rate", "reallife_loyalty_btn_push", "reallife_loyalty_btn_day" }
                .All(k => english.ContainsKey(k) && page.Contains("<b>" + english[k] + "</b>")), "loyalty texts: the Guide page names the tab, the option and the buttons as the game shows them");
        }
    }
}
