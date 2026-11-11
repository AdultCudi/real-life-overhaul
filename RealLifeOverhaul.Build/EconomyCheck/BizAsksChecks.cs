using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.ParkSpaces;
using RealLifeOverhaul.Ui;

// The smaller business asks (run with --bizasks). Parking spaces (Scripts/ParkSpaces): where every number comes from, a car park's spaces and who holds them, the price and the
// rent, no profit from buying and selling whatever the market does, no space held twice, a full car park, every refusal before any money, the rent's terms over a year and
// after time away, the car-hours counted once and paid back once, a car park that goes, the rival tycoons by the same rules, the save (an old one, a newer one, a damaged one,
// a decimal comma, in the middle of a term), the page and its buttons, the sources. The showroom's suppliers (Scripts/Economy/Dealer/Supply): the table, a price and a
// delivery from the listing alone, nothing to gain between suppliers and the trade, a manager's standing orders inside every cap and never twice, the other dealers by the
// same rules, the save, the page and its steps, the sources. Then the texts of both in every language.
internal static partial class Program
{
    private static readonly string[] BizAsksLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static void BizAsks()
    {
        SpaceNumberChecks();
        SpaceParkChecks();
        SpacePriceChecks();
        SpaceTakeChecks();
        SpaceRentChecks();
        SpaceHourChecks();
        SpaceDayChecks();
        SpaceGoneChecks();
        SpaceRivalChecks();
        SpaceStoreChecks();
        SpacePageChecks();
        SpaceSourceChecks();
        SupplyTableChecks();
        SupplyPriceChecks();
        SupplyPlanChecks();
        SupplyMorningChecks();
        SupplyRivalChecks();
        SupplyDealersChecks();
        SupplyStoreChecks();
        SupplyPageChecks();
        SupplySourceChecks();
        BizAsksTextChecks();
    }

    // ---- parking spaces

    private static SpacePark BizPark(int number, int capacity, int share, SpaceOwner owner = SpaceOwner.City, int market = 1000)
    {
        var p = new SpacePark { Number = number, Name = "Car park " + number, District = "Midtown", Capacity = capacity, CityShare = share, RentMonth = SpaceRules.RentMonth(1000), Owner = owner };
        p.Price = SpaceRules.Price(p.RentMonth, 120, market);
        return p;
    }

    private static void SpaceNumberChecks()
    {
        Check(SpaceCatalogue.HourFee == 25, "parking spaces numbers: the hourly charge paid back is the game's own 25 an hour");
        Check(SpaceCatalogue.RentDays == CarParkRules.LeaseDays && SpaceCatalogue.RentShare == CarParkRules.LeaseShare, "parking spaces numbers: a rented space's term and share are a leased bay's at the mod's car parks");
        Check(SpaceCatalogue.Yield == EstateCatalogue.YieldMiddle[(int)EstateUse.Storage], "parking spaces numbers: the yield a price is reckoned at is the property market's for storage");
        Check(SpaceCatalogue.BackFee == EstateCatalogue.AgentFee, "parking spaces numbers: the fee on a space sold back is the property market's agent's fee");
        Check(SpaceRules.RentMonth(1000) == (int)(CarParkRules.LeaseMonthly(SpaceCatalogue.HourFee * 100) / 100L) && SpaceRules.RentMonth(1000) == 3060,
            "parking spaces numbers: a term's rent in a middling district is what a bay leased at the hourly charge costs (3,060)");
        Check(SpaceCatalogue.HeldPerPark <= SpaceCatalogue.HeldMost && SpaceCatalogue.CapacityLeast >= SpaceCatalogue.HeldPerPark && SpaceCatalogue.GoneDays >= 1 && SpaceCatalogue.RivalEvery >= 1,
            "parking spaces numbers: the limits fit each other");
    }

    private static void SpaceParkChecks()
    {
        bool code = true;
        for (int use = 0; use < (int)BuildUse.Count; use++)
        {
            for (int sqm = 0; sqm <= 20000; sqm += 250)
            {
                int asked = PlansRules.ParkingNeeded((BuildUse)use, sqm), have = SpaceRules.Capacity(use, sqm);
                code &= have == Math.Max(SpaceCatalogue.CapacityLeast, Math.Min(SpaceCatalogue.CapacityMost, asked));
            }
        }
        Check(code, "parking spaces car park: its spaces are what the building code asks of the building over it, within the least and the most");
        Check(SpaceRules.Capacity(0, 0) == SpaceCatalogue.CapacityLeast && SpaceRules.Capacity(1, 1000000) == SpaceCatalogue.CapacityMost && SpaceRules.Capacity(99, -5) == SpaceCatalogue.CapacityLeast,
            "parking spaces car park: the smallest and the largest building, and a use and area out of range");
        bool rising = true;
        for (int d = 0; d < 60; d++)
        {
            rising &= SpaceRules.CityShare(d + 1) >= SpaceRules.CityShare(d) && SpaceRules.CityShare(d) >= SpaceCatalogue.CityShareLeast && SpaceRules.CityShare(d) <= SpaceCatalogue.CityShareMost;
        }
        Check(rising && SpaceRules.CityShare(double.NaN) == SpaceCatalogue.CityShareLeast && SpaceRules.CityShare(-4) == SpaceCatalogue.CityShareLeast && SpaceRules.CityShare(15) == 800,
            "parking spaces car park: the city's people hold more where the street costs more, within the least and the most (800 at 15 an hour)");
        int held = 0, all = 0, changed = 0;
        var answers = new Dictionary<long, bool>();
        for (int park = 1; park <= 40; park++)
        {
            for (int slot = 1; slot <= 50; slot++)
            {
                bool now = SpaceRules.CityHolds(991, park, slot, 100, 700);
                held += now ? 1 : 0;
                all++;
                changed += now != SpaceRules.CityHolds(991, park, slot, 100 + SpaceCatalogue.CityTurnDays * 3, 700) ? 1 : 0;
                answers[park * 1000L + slot] = now;
            }
        }
        // Asked again in the opposite order, after other seeds, days and shares were asked about: a rule with a memory (a counter, a cache, a random) would give other answers.
        int differ = 0;
        for (int park = 40; park >= 1; park--)
        {
            for (int slot = 50; slot >= 1; slot--)
            {
                SpaceRules.CityHolds(12 + slot, park, slot, 7 * park, 100 * (slot % 10));
                differ += SpaceRules.CityHolds(991, park, slot, 100, 700) != answers[park * 1000L + slot] ? 1 : 0;
            }
        }
        Check(differ == 0 && answers.Count == all, "parking spaces car park: who holds a space on a day is the same whenever and in whatever order it is asked (" + differ + " answers differ)");
        Check(Math.Abs(held * 1000 / all - 700) <= 40, "parking spaces car park: the city's people hold their share of the spaces (" + held * 1000 / all + " of 700 thousandths)");
        Check(changed > all / 10 && changed < all * 6 / 10, "parking spaces car park: spaces change hands over the months, and most do not at once (" + changed + " of " + all + ")");
        Check(!SpaceRules.CityHolds(5, 2, 3, 77, 0) && SpaceRules.CityHolds(5, 2, 3, 77, 1000), "parking spaces car park: no share holds nothing and the whole share holds everything");
        var s = new SpaceState { Seed = 123 };
        SpacePark p = BizPark(4, 30, 700);
        SpaceRules.Counts(s, p, 50, out int player, out int rivals, out int city, out int free);
        Check(player == 0 && rivals == 0 && city + free == p.Capacity && free > 0, "parking spaces car park: every space is somebody's or free, and the counts add up to the car park");
        int first = SpaceRules.FreeSlot(s, p, 50);
        Check(first >= 1 && first <= p.Capacity && !SpaceRules.CityHolds(s.Seed, p.Number, first, 50, p.CityShare), "parking spaces car park: the space offered is one nobody holds");
        Check(SpaceRules.FreeSlot(s, BizPark(5, 30, 1000), 50) == 0 && SpaceRules.FreeSlot(s, null, 50) == 0, "parking spaces car park: a full car park and no car park offer no space");
    }

    private static void SpacePriceChecks()
    {
        Check(SpaceRules.Price(3060, 60, 1000) == 72900 && SpaceRules.Price(3060, 120, 1000) == 145700, "parking spaces price: the rent of a year at the market's yield (72,900 in a year of 60 days, 145,700 in one of 120)");
        Check(SpaceRules.RentMonth(840) < SpaceRules.RentMonth(1000) && SpaceRules.RentMonth(1160) > SpaceRules.RentMonth(1000) && SpaceRules.RentMonth(1160) % SpaceCatalogue.RoundRent == 0,
            "parking spaces price: the rent follows the district, named in tens");
        Check(SpaceRules.Price(3060, 120, 1200) > SpaceRules.Price(3060, 120, 1000) && SpaceRules.Price(3060, 120, 800) < SpaceRules.Price(3060, 120, 1000)
              && SpaceRules.Price(3060, 120, 1000) % SpaceCatalogue.RoundPrice == 0 && SpaceRules.Price(0, 120, 1000) == SpaceCatalogue.RoundPrice, "parking spaces price: the price follows the property market, named in hundreds");
        bool never = true;
        long worst = long.MinValue;
        for (long paid = 100; paid <= 2000000; paid = paid * 3 / 2 + 100)
        {
            for (long now = 100; now <= 4000000; now = now * 3 / 2 + 100)
            {
                long back = SpaceRules.Back(paid, now);
                never &= back < paid && back <= now && back >= 0;
                worst = Math.Max(worst, back - paid);
            }
        }
        Check(never && worst < 0, "parking spaces no profit: the owner never pays as much for a space as was paid for it, whatever its price today");
        Check(SpaceRules.Back(100000, 100000) == 100000L * (1000 - SpaceCatalogue.BackFee) / 1000 && SpaceRules.Back(100000, 50000) == 50000L * (1000 - SpaceCatalogue.BackFee) / 1000
              && SpaceRules.Back(-5, 100) == 0, "parking spaces no profit: the lower of what was paid and today's price, less the fee");
        // A loop of buying low and selling high over a property market that swings: the cash only ever falls.
        var s = new SpaceState { Seed = 77 };
        long cash = 10000000, start = cash;
        bool fell = true;
        for (int round = 0; round < 200; round++)
        {
            int low = 600 + round % 7 * 40, high = 1400 + round % 5 * 100;
            SpacePark cheap = BizPark(9, 20, 0, SpaceOwner.City, low), dear = BizPark(9, 20, 0, SpaceOwner.City, high);
            long before = cash;
            if (SpaceRules.MayTake(s, cheap, round, true, false, cash, cheap.Price, out int slot) != SpaceRefusal.None)
            {
                fell = false;
                break;
            }
            SpaceHolding h = SpaceRules.Take(s, cheap, slot, SpaceHolding.PlayerIs, round, cheap.Price, 0);
            cash -= cheap.Price;
            cash += SpaceRules.Sell(s, h, dear.Price);
            fell &= cash < before && s.Held.Count == 0;
        }
        Check(fell && cash < start, "parking spaces no profit: two hundred rounds of buying in a slump and selling in a boom lose money every round");
    }

    private static void SpaceTakeChecks()
    {
        var s = new SpaceState { Seed = 31 };
        SpacePark p = BizPark(2, 12, 0);
        Check(SpaceRules.MayTake(null, p, 5, true, false, 1000000, p.Price, out int _) == SpaceRefusal.Off && SpaceRules.MayTake(s, p, 5, false, false, 1000000, p.Price, out int _) == SpaceRefusal.Off,
            "parking spaces refusals: with the option off nothing is taken");
        Check(SpaceRules.MayTake(s, p, 5, true, true, 1000000, p.Price, out int _) == SpaceRefusal.Session, "parking spaces refusals: in a shared city nothing is taken");
        Check(SpaceRules.MayTake(s, null, 5, true, false, 1000000, 100, out int _) == SpaceRefusal.NoPark, "parking spaces refusals: a car park that is not there");
        Check(SpaceRules.MayTake(s, BizPark(2, 12, 0, SpaceOwner.Player), 5, true, false, 1000000, p.Price, out int _) == SpaceRefusal.OwnBuilding, "parking spaces refusals: nothing is bought in a building of the player's own");
        Check(SpaceRules.MayTake(s, p, 5, true, false, p.Price - 1, p.Price, out int _) == SpaceRefusal.Cash && SpaceRules.MayTake(s, p, 5, true, false, p.Price, p.Price, out int ok) == SpaceRefusal.None && ok == 1,
            "parking spaces refusals: the cash must cover the price, and the first free space is the one offered");
        Check(s.Held.Count == 0, "parking spaces refusals: a refusal changes nothing");
        // No space twice.
        SpaceHolding first = SpaceRules.Take(s, p, 1, SpaceHolding.PlayerIs, 5, p.Price, 0);
        Check(first != null && SpaceRules.Take(s, p, 1, SpaceHolding.PlayerIs, 5, p.Price, 0) == null && SpaceRules.Take(s, p, 1, 2, 5, p.Price, 0) == null && s.Held.Count == 1,
            "parking spaces no space twice: a space held is not taken again, by the player or a tycoon");
        Check(SpaceRules.Take(s, p, 0, SpaceHolding.PlayerIs, 5, p.Price, 0) == null && SpaceRules.Take(s, p, p.Capacity + 1, SpaceHolding.PlayerIs, 5, p.Price, 0) == null
              && SpaceRules.Take(s, p, 2, SpaceHolding.PlayerIs, 5, 0, 0) == null && SpaceRules.Take(s, p, 2, SpaceHolding.PlayerIs, 5, 100, 100) == null,
            "parking spaces no space twice: a space outside the car park, and one neither bought nor rented, is not taken");
        SpacePark crowded = BizPark(3, 40, 800);
        var c = new SpaceState { Seed = 8 };
        int cityHeld = 0;
        for (int slot = 1; slot <= crowded.Capacity; slot++)
        {
            bool theirs = SpaceRules.CityHolds(c.Seed, crowded.Number, slot, 9, crowded.CityShare);
            cityHeld += theirs ? 1 : 0;
            Check(!theirs || SpaceRules.Take(c, crowded, slot, SpaceHolding.PlayerIs, 9, 100, 0) == null, "parking spaces no space twice: a space one of the city's people holds is not taken");
        }
        // The per car park and the overall limit, and a car park filled to the last space.
        var limits = new SpaceState { Seed = 31 };
        int taken = 0;
        while (SpaceRules.MayTake(limits, p, 5, true, false, long.MaxValue / 8, p.Price, out int slot2) == SpaceRefusal.None)
        {
            SpaceRules.Take(limits, p, slot2, SpaceHolding.PlayerIs, 5, p.Price, 0);
            taken++;
        }
        Check(taken == SpaceCatalogue.HeldPerPark && SpaceRules.MayTake(limits, p, 5, true, false, long.MaxValue / 8, p.Price, out int _) == SpaceRefusal.ParkLimit,
            "parking spaces limits: no more than " + SpaceCatalogue.HeldPerPark + " spaces in one car park");
        for (int park = 20; limits.Count(0, SpaceHolding.PlayerIs) < SpaceCatalogue.HeldMost; park++)
        {
            SpacePark other = BizPark(park, 12, 0);
            SpaceRules.MayTake(limits, other, 5, true, false, long.MaxValue / 8, other.Price, out int slot3);
            SpaceRules.Take(limits, other, slot3, SpaceHolding.PlayerIs, 5, other.Price, 0);
        }
        Check(limits.Count(0, SpaceHolding.PlayerIs) == SpaceCatalogue.HeldMost && SpaceRules.MayTake(limits, BizPark(90, 12, 0), 5, true, false, long.MaxValue / 8, 100, out int _) == SpaceRefusal.Limit,
            "parking spaces limits: no more than " + SpaceCatalogue.HeldMost + " spaces in all");
        var full = new SpaceState { Seed = 4 };
        SpacePark small = BizPark(7, 8, 0);
        for (int t = 0; t < 8; t++)
        {
            SpaceRules.Take(full, small, SpaceRules.FreeSlot(full, small, 3), t, 3, small.Price, 0);
        }
        SpaceRules.Counts(full, small, 3, out int mine, out int theirs2, out int city, out int free);
        Check(mine == 0 && theirs2 == 8 && city == 0 && free == 0 && SpaceRules.MayTake(full, small, 3, true, false, long.MaxValue / 8, small.Price, out int none) == SpaceRefusal.Full && none == 0,
            "parking spaces a full building: with every space held, none is offered and the purchase is refused before any money");
        // Selling.
        Check(SpaceRules.MaySell(s, first, p, true, false) == SpaceRefusal.None && SpaceRules.MaySell(s, first, p, true, true) == SpaceRefusal.Session && SpaceRules.MaySell(s, first, p, false, false) == SpaceRefusal.Off
              && SpaceRules.MaySell(s, first, null, true, false) == SpaceRefusal.NoPark && SpaceRules.MaySell(s, null, p, true, false) == SpaceRefusal.NotHeld
              && SpaceRules.MaySell(s, new SpaceHolding { Park = 2, Slot = 9, Paid = 5 }, p, true, false) == SpaceRefusal.NotHeld
              && SpaceRules.MaySell(s, first, BizPark(2, 12, 0, SpaceOwner.Player), true, false) == SpaceRefusal.OwnBuilding, "parking spaces selling: only a space that is held, in a car park that stands, to an owner who is not the player");
        long back = SpaceRules.Sell(s, first, p.Price);
        Check(back == SpaceRules.Back(first.Paid, p.Price) && s.Held.Count == 0 && SpaceRules.Sell(s, first, p.Price) == 0, "parking spaces selling: a space is sold once, for what the owner pays");
    }

    private static void SpaceRentChecks()
    {
        var s = new SpaceState { Seed = 6 };
        SpacePark p = BizPark(2, 12, 0);
        SpaceHolding h = SpaceRules.Take(s, p, 1, SpaceHolding.PlayerIs, 10, 0, p.RentMonth);
        Check(h != null && h.Rented && h.PaidTo == 10 + SpaceCatalogue.RentDays - 1 && !SpaceRules.RentDue(h, h.PaidTo) && SpaceRules.RentDue(h, h.PaidTo + 1), "parking spaces rent: the first term is paid for, and the next is due the day after it");
        Check(SpaceRules.MaySell(s, h, p, true, false) == SpaceRefusal.Rented && SpaceRules.Sell(s, h, p.Price) == 0 && s.Held.Count == 1, "parking spaces rent: a rented space is never sold");
        // A year and more of terms, with a save and a load in the middle of one: the same days, the same number of payments.
        int paid = 0, paidLoaded = 0;
        SpaceState loaded = null;
        SpaceHolding hl = null;
        for (int day = 11; day <= 410; day++)
        {
            if (day == 57)
            {
                loaded = SpaceStore.Read(SpaceStore.Write(s), out int _);
                hl = loaded.Held[0];
                paidLoaded = paid;
            }
            if (SpaceRules.RentDue(h, day))
            {
                SpaceRules.RentPaid(h, day);
                paid++;
            }
            if (hl != null && SpaceRules.RentDue(hl, day))
            {
                SpaceRules.RentPaid(hl, day);
                paidLoaded++;
            }
        }
        Check(paid == (410 - 10) / SpaceCatalogue.RentDays && h.PaidTo == 10 + (paid + 1) * SpaceCatalogue.RentDays - 1, "parking spaces rent: a term every " + SpaceCatalogue.RentDays + " days over more than a year, none skipped and none twice (" + paid + ")");
        Check(hl != null && paidLoaded == paid && hl.PaidTo == h.PaidTo, "parking spaces rent: a save and a load in the middle of a term changes no day and no payment");
        // Time away (the option off, or the save not played): no term of it is asked for.
        var away = new SpaceHolding { Park = 2, Slot = 2, Rent = 3060, Day = 10, PaidTo = 39 };
        SpaceRules.RentPaid(away, 200);
        Check(away.PaidTo == 199 + SpaceCatalogue.RentDays, "parking spaces rent: after time away one term is paid from today, and none of the time away");
        Check(SpaceRules.SetEnding(s, h, true, true) == SpaceRefusal.None && h.Ending && SpaceRules.SetEnding(s, h, false, true) == SpaceRefusal.None && !h.Ending
              && SpaceRules.SetEnding(s, h, true, false) == SpaceRefusal.Off && SpaceRules.SetEnding(s, null, true, true) == SpaceRefusal.NotHeld, "parking spaces rent: a rental is given up and kept again, only while the option is on");
        SpaceHolding bought = SpaceRules.Take(s, p, 2, SpaceHolding.PlayerIs, 10, p.Price, 0);
        Check(SpaceRules.SetEnding(s, bought, true, true) == SpaceRefusal.Owned && !bought.Ending && !SpaceRules.RentDue(bought, 100000), "parking spaces rent: a bought space has no rental and no rent, ever");
    }

    private static void SpaceHourChecks()
    {
        var s = new SpaceState { Seed = 12 };
        SpacePark p = BizPark(2, 12, 0);
        Check(SpaceRules.Entitled(s, p, 5) == 0 && SpaceRules.Covered(SpaceRules.Entitled(s, p, 5), 3) == 0, "parking spaces hours: without a space no car is covered");
        SpaceRules.Take(s, p, 1, SpaceHolding.PlayerIs, 5, p.Price, 0);
        Check(SpaceRules.Entitled(s, p, 5) == 1 && SpaceRules.Covered(1, 3) == 1 && SpaceRules.Covered(1, 0) == 0 && SpaceRules.Covered(2, 1) == 1 && SpaceRules.Covered(-1, 5) == 0,
            "parking spaces hours: one car for each space, and no more cars than stand there");
        SpaceRules.Take(s, p, 2, 1, 5, p.Price, 0);
        Check(SpaceRules.Entitled(s, p, 5) == 1, "parking spaces hours: a tycoon's space covers none of the player's cars");
        SpacePark own = BizPark(8, 10, 0, SpaceOwner.Player);
        SpaceRules.Take(s, own, 3, 2, 5, own.Price, 0);
        Check(SpaceRules.Entitled(s, own, 5) == 9 && SpaceRules.Entitled(s, null, 5) == 0, "parking spaces hours: in a building of the player's own every space nobody else holds is theirs");
        SpaceHolding rented = SpaceRules.Take(s, p, 3, SpaceHolding.PlayerIs, 5, 0, p.RentMonth);
        Check(SpaceRules.Entitled(s, p, rented.PaidTo) == 2 && SpaceRules.Entitled(s, p, rented.PaidTo + 1) == 1, "parking spaces hours: a rented space covers the days its rent is paid for and not the day after");
        SpaceState sk = new SpaceState();
        SpaceRules.AddHour(sk, 5, 2, null);
        SpaceRules.AddHour(sk, 5, 0, "v");
        Check(sk.Pending.Count == 0, "parking spaces hours: no hour is counted for no vehicle or no car park");
    }

    private static void SpaceGoneChecks()
    {
        var owned = new SpaceHolding { Park = 5, Slot = 1, Paid = 90000, Day = 10, PriceSeen = 90000 };
        SpaceRules.Seen(owned, true, 20);
        Check(owned.Missing == 0 && !SpaceRules.Gone(owned, 500), "parking spaces a car park gone: one that stands is never settled");
        SpaceRules.Seen(owned, false, 20);
        SpaceRules.Seen(owned, false, 21);
        Check(owned.Missing == 20 && !SpaceRules.Gone(owned, 20 + SpaceCatalogue.GoneDays - 1) && SpaceRules.Gone(owned, 20 + SpaceCatalogue.GoneDays), "parking spaces a car park gone: missing for " + SpaceCatalogue.GoneDays + " days before it is settled");
        SpaceRules.Seen(owned, true, 22);
        Check(owned.Missing == 0 && !SpaceRules.Gone(owned, 400), "parking spaces a car park gone: one that stands again (rebuilt with its number, or stood late at a load) keeps its spaces");
        Check(SpaceRules.Close(owned, SpaceEnd.CarParkGone, 0, 30) == owned.Paid, "parking spaces a car park gone: a bought space whose car park somebody else took away is paid back at what was paid, no more and no fee");
        var rented = new SpaceHolding { Park = 5, Slot = 2, Rent = 3000, Day = 10, PaidTo = 39, Missing = 25 };
        Check(SpaceRules.Close(rented, SpaceEnd.CarParkGone, 0, 28) == 3000L * (39 - 25 + 1) / SpaceCatalogue.RentDays && SpaceRules.Close(rented, SpaceEnd.CarParkGone, 0, 60) == 3000L * (39 - 25 + 1) / SpaceCatalogue.RentDays
              && SpaceRules.Close(new SpaceHolding { Rent = 3000, PaidTo = 5, Missing = 30 }, SpaceEnd.CarParkGone, 0, 60) == 0 && SpaceRules.Close(new SpaceHolding { Rent = 3000, PaidTo = 900, Missing = 1 }, SpaceEnd.CarParkGone, 0, 1) <= 3000
              && SpaceRules.Close(null, SpaceEnd.CarParkGone, 0, 5) == 0 && SpaceRules.Close(rented, SpaceEnd.OwnBuilding, 0, 28) == 0 && SpaceRules.Close(rented, SpaceEnd.SoldBack, 0, 28) == 0,
            "parking spaces a car park gone: a rented space is paid back for the days unused since the car park went, never more than a term, nothing when the building became the player's");
        var seen = new SpaceHolding { Park = 5, Slot = 3, Paid = 90000, Day = 10, PriceSeen = 70000, OwnSeen = true };
        var unseen = new SpaceHolding { Park = 5, Slot = 4, Paid = 90000, Day = 10 };
        Check(SpaceRules.Close(seen, SpaceEnd.CarParkGone, 0, 30) == SpaceRules.Back(90000, 70000) && SpaceRules.Close(seen, SpaceEnd.SoldBack, 50000, 30) == SpaceRules.Back(90000, 50000)
              && SpaceRules.Close(seen, SpaceEnd.OwnBuilding, 120000, 30) == SpaceRules.Back(90000, 120000) && SpaceRules.Close(unseen, SpaceEnd.CarParkGone, 0, 30) == 90000,
            "parking spaces ending: one rule for every end of a bought space, the lower of paid and the price less the fee where the player did it, the full price paid where somebody else took the car park away");
        SpacePark noted = BizPark(5, 20, 0, SpaceOwner.Player, 1100);
        var h = new SpaceHolding { Park = 5, Slot = 1, Paid = 50000 };
        SpaceRules.Noted(h, noted);
        SpaceRules.Noted(h, null);
        Check(h.PriceSeen == noted.Price && h.OwnSeen && !SpaceRules.Moved(-1, 5, 0, 3) && !SpaceRules.Moved(500, 500, 3, 3) && SpaceRules.Moved(500, 501, 3, 3) && SpaceRules.Moved(500, 500, 3, 4) && !SpaceRules.Moved(500, 500, 0, 9),
            "parking spaces ending: the price and the owner last seen are kept, and a price or a space moved since the question box is told apart from one that is not");
    }

    private static void SpaceRivalChecks()
    {
        var parks = new List<SpacePark> { BizPark(1, 10, 500), BizPark(2, 10, 500, SpaceOwner.Player), BizPark(3, 10, 500, SpaceOwner.Rival) };
        long price = parks[0].Price;
        var s = new SpaceState { Seed = 2024 };
        var again = new SpaceState { Seed = 2024 };
        long[] worth = { price * 40, price * 40, -1, price / 2 }, floor = { price * 10, price * 10, 0, 0 }, start = (long[])worth.Clone();
        var moves = new List<SpaceMove>();
        var movesAgain = new List<SpaceMove>();
        bool rules = true, same = true;
        int bought = 0, sold = 0;
        long paid = 0, got = 0, drift = 0;
        bool noGain = true;
        for (int day = 1; day <= 900; day++)
        {
            int before = moves.Count;
            SpaceRules.RivalLook(s, parks, day, worth, floor, moves);
            SpaceRules.RivalLook(again, parks, day, (long[])worth.Clone(), floor, movesAgain);
            for (int i = before; i < moves.Count; i++)
            {
                SpaceMove m = moves[i];
                rules &= m.Park != 2 && m.Tycoon != 2 && m.Tycoon != 3 && m.Amount >= 0;
                if (m.Sold)
                {
                    sold++;
                    got += m.Amount;
                    rules &= m.Amount == SpaceRules.Back(price, SpaceRules.Find(parks, m.Park).Price) && m.Amount < price && m.Worth == m.Amount - price;
                    worth[m.Tycoon] += m.Worth;   // what they own: the space left, the money came in, and the fee is lost
                    drift += m.Worth;
                }
                else
                {
                    bought++;
                    paid += m.Amount;
                    rules &= worth[m.Tycoon] - m.Amount >= floor[m.Tycoon] && m.Amount == SpaceRules.Find(parks, m.Park).Price;
                    noGain &= m.Worth == 0;   // a space bought is worth what was paid: their worth does not move, so the daily growth towards their aim has nothing to refill
                    worth[m.Tycoon] += m.Worth;
                }
                noGain &= m.Worth <= 0;
            }
            for (int t = 0; t < worth.Length; t++)
            {
                rules &= s.Count(0, t) <= 1;
            }
            for (int i = 0; i < s.Held.Count; i++)
            {
                for (int k = 0; k < i; k++)
                {
                    rules &= s.Held[i].Park != s.Held[k].Park || s.Held[i].Slot != s.Held[k].Slot;
                }
                rules &= s.Held[i].Park != 2 && !SpaceRules.CityHolds(s.Seed, s.Held[i].Park, s.Held[i].Slot, s.Held[i].Day, 500);
            }
        }
        same = moves.Count == movesAgain.Count && s.Held.Count == again.Held.Count;
        for (int i = 0; same && i < moves.Count; i++)
        {
            same = moves[i].Tycoon == movesAgain[i].Tycoon && moves[i].Park == movesAgain[i].Park && moves[i].Slot == movesAgain[i].Slot && moves[i].Amount == movesAgain[i].Amount && moves[i].Sold == movesAgain[i].Sold;
        }
        Check(bought >= 2 && sold >= 1, "parking spaces tycoons: over the years they buy spaces and sell some back (" + bought + " bought, " + sold + " sold)");
        Check(rules, "parking spaces tycoons: one space each at most, at the player's price, never under their floor, never a space held, never in a building of the player's, a sale for what the owner pays");
        Check(same, "parking spaces tycoons: the same save and the same days give the same moves on every machine");
        Check(noGain && start[0] + start[1] + drift == worth[0] + worth[1] && drift <= 0 && paid >= got && worth[2] == -1 && worth[3] == start[3],
            "parking spaces tycoons: buying a space moves no worth (so growth can refill nothing and the space is not free), selling one loses the fee and never gains (" + drift + " in all); one not in play and one who cannot afford a space do nothing");
        var none = new List<SpaceMove>();
        SpaceRules.RivalLook(new SpaceState { Seed = 1 }, new List<SpacePark>(), 20, new long[] { 1000000000 }, null, none);
        SpaceRules.RivalLook(new SpaceState { Seed = 1 }, new List<SpacePark> { BizPark(2, 10, 0, SpaceOwner.Player) }, 20, new long[] { 1000000000 }, null, none);
        for (int day = 0; day < 200; day++)
        {
            SpaceRules.RivalLook(new SpaceState { Seed = 1 }, new List<SpacePark> { BizPark(2, 10, 0, SpaceOwner.Player) }, day, new long[] { 1000000000 }, null, none);
        }
        Check(none.Count == 0, "parking spaces tycoons: with no car park, or only the player's own building, nothing is bought");
    }

    private static void SpaceStoreChecks()
    {
        Check(SpaceStore.Load(new Dictionary<string, string>(), out int v0) == null && v0 == 0 && SpaceStore.Load(null, out int _) == null && SpaceStore.Read(string.Empty, out int _) == null
              && SpaceStore.Read("something else", out int _) == null, "parking spaces save: a save from before the feature has no line, and nothing is held");
        SpaceState oldHours = SpaceStore.Read("v=1^h:5|20|481|0|0^s:3|4|-1|145700|0|250|0|0|0^c:3|17", out int _);
        Check(oldHours != null && oldHours.Held.Count == 1 && oldHours.Pending.Count == 0 && oldHours.Held[0].PriceSeen == 0 && SpaceRules.Close(oldHours.Held[0], SpaceEnd.CarParkGone, 0, 30) == 145700,
            "parking spaces save: an earlier line with its hours counted by car park has none to pay back (it cannot be told that the game charged them), and a space with no price seen is settled at what was paid");
        var s = new SpaceState { Seed = 55, LastDay = 301, LastHour = 301 * 24 + 7, RefundedTotal = 12345678901L, PaidTotal = -4321 };
        s.Held.Add(new SpaceHolding { Park = 3, Slot = 4, Who = SpaceHolding.PlayerIs, Paid = 145700, Day = 250, PriceSeen = 130500, OwnSeen = true });
        s.Held.Add(new SpaceHolding { Park = 1000123, Slot = 9, Who = SpaceHolding.PlayerIs, Rent = 3060, Day = 280, PaidTo = 309, Ending = true, Missing = 300 });
        s.Held.Add(new SpaceHolding { Park = 3, Slot = 5, Who = 2, Paid = 99000, Day = 100 });
        for (int i = 0; i < 17; i++)
        {
            SpaceRules.AddHour(s, 301, 3, "vehicle one");
        }
        SpaceRules.AddHour(s, 300, 3, "a|b;c^d%e");
        string line = SpaceStore.Write(s);
        SpaceState r = SpaceStore.Read(line, out int version);
        Check(version == SpaceStore.Version && r != null && SpaceStore.Write(r) == line, "parking spaces save: a line written, read and written again is the same line");
        Check(r.Seed == 55 && r.LastDay == 301 && r.LastHour == 301 * 24 + 7 && r.RefundedTotal == 12345678901L && r.PaidTotal == -4321 && r.Held.Count == 3 && r.Held[1].Park == 1000123 && r.Held[1].Rented
              && r.Held[1].PaidTo == 309 && r.Held[1].Ending && r.Held[1].Missing == 300 && r.Held[2].Who == 2 && r.Held[0].Paid == 145700 && r.Held[0].PriceSeen == 130500 && r.Held[0].OwnSeen && !r.Held[1].OwnSeen && r.Hours(3) == 18 && r.Pending.Count == 2
              && r.Pending[0].Day == 301 && r.Pending[0].Vehicle == "vehicle one" && r.Pending[0].Hours == 17 && r.Pending[1].Vehicle == "a|b;c^d%e", "parking spaces save: every field comes back, a vehicle's id of any spelling included");
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Check(SpaceStore.Write(s) == line && SpaceStore.Write(SpaceStore.Read(line, out int _)) == line, "parking spaces save: a language with a decimal comma writes and reads the same line");
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
        SpaceState newer = SpaceStore.Read("v=9^h:55|301|7231|10|20|77|88^s:3|4|-1|145700|0|250|0|0|0|999|1|extra;3|6|-1|0|3060|250|279|0|0^p:301|3|car|17|5^c:3|99^z:what|ever", out int nine);
        Check(nine == 9 && newer != null && newer.Seed == 55 && newer.Held.Count == 2 && newer.Held[0].Paid == 145700 && newer.Held[0].PriceSeen == 999 && newer.Held[0].OwnSeen && newer.Held[1].Rented && newer.Hours(3) == 17, "parking spaces save: a line from a newer version is read as far as it goes");
        SpaceState hurt = SpaceStore.Read("v=1^h:x|y^s:3|4|-1|145700|0|250|0|0|0|-5|1;rubbish;3|4|-1|5|0|1|0|0|0;0|1|-1|5|0|1|0|0|0;3|999|-1|5|0|1|0|0|0;3|7|-9|5|0|1|0|0|0;3|8|-1|0|0|1|0|0|0^p:5|3|v|-50;5|3|w;x|1|v|4;5|0|v|4;5|3||4;5|3|v|99;5|3|v|2;7|3|v|99999",
            out int _);
        Check(hurt != null && hurt.Held.Count == 1 && hurt.Held[0].Slot == 4 && hurt.Held[0].Paid == 145700 && hurt.Held[0].PriceSeen == 0 && hurt.Seed == 0 && hurt.Pending.Count == 2 && hurt.Pending[0].Hours == SpaceCatalogue.HoursMost
              && hurt.Pending[1].Day == 7 && hurt.Pending[1].Hours == SpaceCatalogue.HoursMost && hurt.Hours(0) == 2 * SpaceCatalogue.HoursMost,
            "parking spaces save: a damaged line keeps what can be read, inside its bounds, a space written twice is held once and an hour written twice for a vehicle is kept once");
        var data = new Dictionary<string, string> { { SpaceStore.Key, line } };
        Check(SpaceStore.Load(data, out int loadedVersion) != null && loadedVersion == SpaceStore.Version && SpaceStore.Key == "reallife:parking_spaces", "parking spaces save: the line is the feature's own, under its own key");
    }

    private static void SpacePageChecks()
    {
        UiWords en = ModEnglish();
        UiSamples.SpacePressed press = UiSamples.SpacePress;
        EstateModel m = UiSamples.Estate(en, "space");
        SpaceState world = UiSamples.SpaceWorld(false, out SpacePark park);
        Check(m.Shape == EstateShape.Detail && m.DetailTitle.Contains(park.Name) && m.Strip.Length == 4 && m.Left.Length == 1 && m.Right.Length == 1 && m.Related2.Length == 2 && m.Card,
            "parking spaces page: a car park's page has its figures, the card that takes a space, who holds the spaces and the player's two spaces");
        UiActionModel[] acts = m.Left[0].Actions;
        Check(acts.Length == 2 && acts[1].Kind == UiButtonKind.Primary && acts[0].Kind != UiButtonKind.Primary && !acts[0].Disabled && !acts[1].Disabled, "parking spaces page: one primary action, Buy a space, with Rent a space beside it");
        m.Runs[acts[1].Arg]();
        bool buy = press.Last == "buy" && press.Park == park.Number && press.Slot == SpaceRules.FreeSlot(world, park, 214);
        m.Runs[acts[0].Arg]();
        Check(buy && press.Last == "rent" && press.Park == park.Number, "parking spaces page: Buy a space and Rent a space reach their calls with the car park and the space offered");
        EstateRow owned = m.Related2.First(r => !r.ButtonOff && r.Sub == SpaceWords.Tenure(en, world.Held[0], d => d.ToString())), rented = m.Related2.First(r => r.ButtonRun != owned.ButtonRun);
        m.Runs[owned.ButtonRun]();
        bool sell = press.Last == "sell" && press.Slot == world.Held[0].Slot && press.Back == SpaceRules.Back(world.Held[0].Paid, park.Price) && press.Back < world.Held[0].Paid;
        m.Runs[rented.ButtonRun]();
        Check(sell && press.Last == "ending" && press.Slot == world.Held[1].Slot && press.Ending, "parking spaces page: Sell it back and End the rental reach their calls, the sale at what the owner pays");
        SpaceRules.Counts(world, park, 214, out int player, out int rivals, out int city, out int free);
        Check(m.Right[0].Lines[0].Value == player.ToString() && m.Right[0].Lines[1].Value == rivals.ToString() && m.Right[0].Lines[2].Value == city.ToString() && m.Right[0].Lines[3].Value == free.ToString()
              && player == 2 && rivals == 1 && m.Right[0].Lines[4].Value == "14", "parking spaces page: who holds the spaces, a tycoon among them, and the car-hours to pay back");
        EstateModel full = UiSamples.Estate(en, "space_full");
        UiActionModel[] none = full.Left[0].Actions;
        Check(none.Length == 2 && none[0].Disabled && none[1].Disabled && none[0].Arg < 0 && none[1].Arg < 0 && full.DetailChipTone == UiRole.Warn
              && full.Left[0].Lines.Any(l => l.Kind == UiFactKind.Sentence && l.Value == SpaceWords.Why(en, SpaceRefusal.Full)), "parking spaces page: in a full car park both buttons are off and the page says why");
        UiDataRow row = UiSamples.SpaceMarketRow(en);
        Check(row.Cells.Length == 6 && row.Cells[0] == park.Name && row.Cells[4] == SpaceWords.RowState(en, park, 2, free) && row.Values[0] == (int)UiIcon.Car, "parking spaces page: a car park's row on the Market tab says how many spaces the player holds there");
        Check(SpaceWords.RowState(en, BizPark(1, 10, 0, SpaceOwner.Player), 0, 3) != SpaceWords.RowState(en, park, 0, 3) && SpaceWords.RowState(en, park, 0, 0) != SpaceWords.RowState(en, park, 0, 3),
            "parking spaces page: a row tells the player's own building, free spaces and a full car park apart");
        for (int r = 1; r <= (int)SpaceRefusal.Moved; r++)
        {
            Check(SpaceWords.WhyKey((SpaceRefusal)r) != null && SpaceWords.Why(en, (SpaceRefusal)r).Length > 10 && UiScreens.All.First(d => d.Id == "estate").Keys.Contains(SpaceWords.WhyKey((SpaceRefusal)r)), "parking spaces page: the refusal " + (SpaceRefusal)r + " has its words");
        }
        string app = ModSource(@"Economy\Estate\EstateApp.Spaces.cs"), live = ModSource(@"Ui\Estate\EstateLive.Spaces.cs");
        Check(app.Contains("d.Buy(park, shownPrice, shownSlot)") && app.Contains("d.Rent(park, shownRent, shownSlot)") && app.Contains("d.Sell(park, at, back)") && app.Contains("d.SetEnding(park, at, !ending)")
              && live.Contains("d.Buy(park, shown, slot)") && live.Contains("d.Rent(park, shown, slot)") && live.Contains("d.Sell(park, slot, back)") && live.Contains("d.SetEnding(park, slot, ending)"),
            "parking spaces page: the app in the game's frame and the kit's page press the same calls of the director, each with the price the question box asked about");
        Check(app.Contains("SpaceDirector d = SpacesNow;") && CountOfText(app, "d.Open()") >= 2 && CountOfText(live, "d.Open()") >= 3 && !app.Contains("d.Parks") && !live.Contains("d.Parks")
              && app.Contains("row.NoTotal = true;   // a car park's space is not a building for sale") && CountOfText(app, "row.NoTotal = true;") == 2,
            "parking spaces page: every page reads the car parks again as it is built, never the last day's list, and a space is in no total of the Property app's tables");
        Check(CountOfText(app, "AskSpace(") >= 4 && CountOfText(live, "AskSpace(") >= 4 && app.Contains("GameConfirm.Show(") && live.Contains("GameConfirm.Show("), "parking spaces page: buying, renting and selling are asked first in the game's own question box");
        Check(ModSource(@"Economy\Estate\EstateApp.cs").Contains("WriteSpaceRows(districts, ref selected)") && ModSource(@"Economy\Estate\EstateApp.Property.cs").Contains("WriteSpacesHeld(ref selected)")
              && ModSource(@"Ui\Estate\EstateLive.Market.cs").Contains("SpaceRows(rows, keys, locate)") && ModSource(@"Ui\Estate\EstateLive.Mine.cs").Contains("SpaceHeld(rows, keys)"),
            "parking spaces page: the car parks are rows of the Market tab and the spaces held rows of the Mine tab, in the game's frame and on the kit");
    }

    private static void SpaceSourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"ParkSpaces\SpaceDirector.cs"));
        string[] files = Directory.GetFiles(folder, "*.cs").Concat(new[] { ModPath(@"Economy\Estate\EstateApp.Spaces.cs"), ModPath(@"Ui\Estate\EstateLive.Spaces.cs"),
            ModPath(@"Ui\Estate\SpacePages.cs") }).ToArray();   // the pages stand in the folders of the screens they are part of
        Check(files.Length >= 11, "parking spaces sources: the feature's files are found (" + files.Length + ")");
        foreach (string file in files)
        {
            string text = File.ReadAllText(file), name = System.IO.Path.GetFileName(file);
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("Update()") && !text.Contains("FindObjectsByType") && !text.Contains("FindObjectOfType"), "parking spaces sources: " + name + " books no money outside the one call and has no per-frame code or scene search");
            Check(!text.Contains("TODO") && !text.Contains("NotImplemented") && text.Split('\n').All(l => l.TrimEnd('\r').Length < 200), "parking spaces sources: " + name + " has no stub, and no line of 200 columns");
        }
        string director = ModSource(@"ParkSpaces\SpaceDirector.cs"), game = ModSource(@"ParkSpaces\SpaceGame.cs");
        Check(CountOfText(game, "BizBooks.Book(") == 2 && game.Contains("force: false") && !director.Contains("BizBooks.") && !ModSource(@"ParkSpaces\SpaceRules.cs").Contains("Unity"), "parking spaces sources: money moves only through the mod's one booking call, in the one game file");
        int take = director.IndexOf("private SpaceRefusal Take(int park, bool rent, long shown, int slotShown)", StringComparison.Ordinal);
        int asked = take > 0 ? director.IndexOf("MayTake(park, rent, out int slot)", take, StringComparison.Ordinal) : -1, refused = take > 0 ? director.IndexOf("return Say(r);", take, StringComparison.Ordinal) : -1;
        int moved = take > 0 ? director.IndexOf("SpaceRules.Moved(shown, cost, slotShown, slot)", take, StringComparison.Ordinal) : -1;
        int pays = take > 0 ? director.IndexOf("SpaceGame.Pay(", take, StringComparison.Ordinal) : -1, holds = take > 0 ? director.IndexOf("SpaceRules.Take(_state", take, StringComparison.Ordinal) : -1;
        Check(take > 0 && asked > take && refused > asked && moved > refused && pays > moved && holds > pays && director.Contains("ReadParks();   // today's price and the building's owner as they are now"),
            "parking spaces sources: the city is read again, a purchase whose price or space is not the one asked about is refused, and every refusal comes before any money is taken; the space is only held once it is paid for");
        Check(director.Contains("SpaceGame.Receive(cost, rent ? SpaceGame.RentKey : SpaceGame.BuyKey, p.Name);"), "parking spaces sources: money taken for a space that could not be had is given back");
        Check(director.Contains("GoingPublic.InSession, SpaceGame.Cash") && director.Contains("int moves = session ? 0 : Rivals(day);") && director.Contains("SpaceRules.MaySell(Running ? _state : null, h, p, Running, GoingPublic.InSession)"),
            "parking spaces sources: in a shared city nothing is bought, rented or sold and the tycoons rest");
        Check(director.Contains("if (!Settings.ParkingSpaces)") && director.Contains("PutAway();") && director.Contains("public bool Running => _loaded && !_broken && Settings.ParkingSpaces;")
              && director.Contains("if (!Running || !SpaceGame.Ready)"), "parking spaces off: with the option off nothing is counted, paid back or taken, and the line is kept");
        Check(director.Contains("EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen)") && director.Contains("_onHour = Guard.Wrap(OnHour);") && director.Contains("GlobalEvents.onNewHour -= _onHour;")
              && director.Contains("StartCoroutine(Guard.Routine(Tick()))") && director.Contains("SpaceDay.Hour(_state, _parks, SpaceGame.Now, SpaceGame.Day, SpaceGame.CarsIn, _cars)") && director.Contains("if (SpaceDay.Stale(_state, _parks))"),
            "parking spaces sources: the hour is the game's own event, guarded, taken off again, counted by the rule that counts it once, and the city is read again when a car park is not known");
        Check(director.Contains("SpaceDay.Resume(_state, SpaceGame.Now);   // hours that went by without the feature are not counted") && director.Contains("SpaceGame.OwnersChanged(_parks, _state)") && director.Contains("OwnersLook(day);"),
            "parking spaces sources: hours that went by without the feature are never paid back, and a building the player buys is seen within the second");
        Check(game.Contains("GameDlc.Open(InstanceBehavior<CityManager>.Instance)") && game.Contains("v.parkingState != ParkingState.Legal || v.streetName != CarParkStreet || v.streetNumber != number || v.id == save.ActiveVehicleId")
              && game.Contains("!type.spawnInPlayerObject"), "parking spaces sources: the car parks come from the city's own list, and a car is counted by the game's own test for its hourly charge");
        string dayRules = ModSource(@"ParkSpaces\SpaceDay.cs");
        Check(dayRules.Contains("SpaceRules.Looked(h, p, day);") && dayRules.Contains("SpaceRules.Close(h, SpaceEnd.CarParkGone, 0L, day)") && dayRules.Contains("SpaceRules.Close(h, SpaceEnd.OwnBuilding, p.Price, day)")
              && director.Contains("SpaceDay.Pass(_state, _parks, day, session, SpaceGame.HasVehicle, _books)") && !director.Contains("GonePay") && !ModSource(@"ParkSpaces\SpaceRules.cs").Contains("Unity"),
            "parking spaces sources: a car park gone, smaller or the player's now is settled on the day's pass by one rule for every way a space ends");
        Check(CountOfText(ModSource(@"Wiring\Wiring.cs"), "BizAsksLinks") == 2 && ModSource(@"Rivals\RivalPage.cs").Contains("RivalBridges.SpaceLine") && CountOfText(ModSource(@"Rivals\RivalPage.cs"), "RivalBridges.SpaceLine") == 4,
            "parking spaces sources: the wiring connects and disconnects, and a tycoon's space shows on the rich list and their panel");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("ParkSpaces.SpaceGuide.Register()") && ModSource(@"Core\FeatureOwners.cs").Contains("\"RealLifeOverhaul.ParkSpaces\", \"parking_spaces\"")
              && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"parkspaces\")") && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"parking_spaces\"") && ModSource(@"Core\Settings.cs").Contains("public static bool ParkingSpaces = true;")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("ParkSpaces.SpaceDirector.Instance?.TestBuy()") && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("ParkSpaces.SpaceDirector.Attach(_root, context.Logger)"),
            "parking spaces sources: the feature has its option (on), its Guide page, its owner, its place in the cities list and its tests");
        string day = ModSource(@"Economy\Finance\DayRules.cs");
        Check(day.Contains("\"space_buy\", \"space_sold\", \"space_gone\"") && !day.Contains("\"space_rent\"") && !day.Contains("\"space_refund\""), "parking spaces sources: a space bought or sold is capital in the day's result; its rent and the charges paid back count");
    }

    // ---- the showroom's suppliers

    private static readonly MarketModel[] SupplyModelsOf =
    {
        new MarketModel("eco", 18000f, 1), new MarketModel("saloon", 32000f, 2), new MarketModel("lux", 90000f, 3), new MarketModel("sport", 140000f, 4), new MarketModel("suv", 52000f, 5),
        new MarketModel("van", 38000f, 6), new MarketModel("truck", 80000f, 7)
    };

    private static void SupplyTableChecks()
    {
        Check(CarSupplyCatalogue.Price.Length == CarSupplyCatalogue.Suppliers && CarSupplyCatalogue.Days.Length == CarSupplyCatalogue.Suppliers && CarSupplyCatalogue.NameKeys.Length == CarSupplyCatalogue.Suppliers
              && CarSupplyCatalogue.AboutKeys.Length == CarSupplyCatalogue.Suppliers && CarSupplyCatalogue.KindKeys.Length == CarSupplyCatalogue.Kinds && CarSupplyCatalogue.Price.All(r => r.Length == CarSupplyCatalogue.Kinds),
            "car suppliers table: a price for every supplier and kind, a delivery and a name for every supplier");
        Check(CarSupplyCatalogue.Kinds == (int)CarKind.Van - (int)CarKind.Economy + 1 && CarSupplyCatalogue.KindFirst == (int)CarKind.Economy && CarSupplyRules.KindIndex((int)CarKind.Economy) == 0
              && CarSupplyRules.KindIndex((int)CarKind.Van) == 5 && CarSupplyRules.KindIndex((int)CarKind.Truck) == -1 && CarSupplyRules.KindIndex((int)CarKind.Untouched) == -1,
            "car suppliers table: the kinds are the six a showroom deals in, economy cars to vans");
        Check(CarSupplyCatalogue.Price[CarSupplyCatalogue.Wholesaler].All(x => x == 1000) && CarSupplyCatalogue.Days[CarSupplyCatalogue.Wholesaler] == 0, "car suppliers table: the wholesaler is as it always was, the listed price and on the lot at once");
        bool bounds = true, honest = true, choice = true;
        for (int s = 1; s < CarSupplyCatalogue.Suppliers; s++)
        {
            bool cheaper = false, dearer = false;
            for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
            {
                int price = CarSupplyCatalogue.Price[s][k];
                bounds &= price >= CarSupplyCatalogue.PriceLeast && price <= CarSupplyCatalogue.PriceMost;
                cheaper |= price < 1000;
                dearer |= price > 1000;
            }
            honest &= cheaper && dearer && CarSupplyCatalogue.Days[s] > 0;   // cheaper for some kinds, dearer for others, and never at once
            choice &= CarSupplyCatalogue.Days[s] >= DealerCatalogue.DeliveryDaysLeast && CarSupplyCatalogue.Days[s] <= DealerCatalogue.DeliveryDaysMost;
        }
        Check(bounds && 1000 - CarSupplyCatalogue.PriceLeast <= DealerCatalogue.WholesaleMost - DealerCatalogue.WholesaleLeast && CarSupplyCatalogue.PriceMost - 1000 <= DealerCatalogue.WholesaleMost - DealerCatalogue.WholesaleLeast,
            "car suppliers table: no supplier is further from the wholesaler's price than the wholesaler's own listings span");
        Check(honest && choice, "car suppliers table: every other supplier is cheaper for some kinds and dearer for others, and its cars take the trade's own delivery days");
        for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
        {
            int best = CarSupplyRules.Cheapest(k);
            Check(Enumerable.Range(0, CarSupplyCatalogue.Suppliers).All(s => CarSupplyCatalogue.Price[best][k] <= CarSupplyCatalogue.Price[s][k]) && CarSupplyCatalogue.Price[best][k] < 1000,
                "car suppliers table: a cheaper supplier than the wholesaler for kind " + k + ", and Cheapest finds it");
        }
        Check(CarSupplyRules.Cheapest(-1) == CarSupplyCatalogue.Wholesaler && CarSupplyRules.Cheapest(99) == CarSupplyCatalogue.Wholesaler, "car suppliers table: a kind unknown is the wholesaler's");
    }

    private static void SupplyPriceChecks()
    {
        var off = new CarSupplyState();
        off.Supplier[0] = CarSupplyCatalogue.Importer;
        Check(CarSupplyRules.SupplierFor(off, 0, true) == CarSupplyCatalogue.Importer && CarSupplyRules.SupplierFor(off, 0, false) == CarSupplyCatalogue.Wholesaler && CarSupplyRules.SupplierFor(null, 0, true) == CarSupplyCatalogue.Wholesaler
              && CarSupplyRules.SupplierFor(off, -1, true) == CarSupplyCatalogue.Wholesaler && CarSupplyRules.SupplierFor(off, 6, true) == CarSupplyCatalogue.Wholesaler,
            "car suppliers price: the supplier chosen while the option is on, the wholesaler with it off, with no state and for a kind unknown");
        bool before = true, named = true;
        for (int listed = 5000; listed <= 400000; listed += 4350)
        {
            int asked = DealerCatalogue.Named(listed);
            for (int rep = 0; rep <= DealerCatalogue.RepMost; rep += 250)
            {
                for (int k = -1; k <= CarSupplyCatalogue.Kinds; k++)
                {
                    before &= CarSupplyRules.ShowroomPrice(asked, CarSupplyCatalogue.Wholesaler, k, rep) == DealerRules.WholesalePrice(asked, rep)
                              && CarSupplyRules.ShowroomPrice(asked, CarSupplyRules.SupplierFor(new CarSupplyState(), k, true), k, rep) == DealerRules.WholesalePrice(asked, rep);
                }
                for (int s = 1; s < CarSupplyCatalogue.Suppliers; s++)
                {
                    for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
                    {
                        int price = CarSupplyRules.Price(asked, s, k);
                        named &= price == DealerCatalogue.Named(DealerCatalogue.Share(asked, CarSupplyCatalogue.Price[s][k])) && price % DealerCatalogue.Round == 0 && price > 0
                                 && CarSupplyRules.ShowroomPrice(asked, s, k, rep) == DealerRules.WholesalePrice(price, rep);
                    }
                }
            }
        }
        Check(before, "car suppliers old saves: with nothing chosen, and from the wholesaler, a new car costs exactly what it did before");
        Check(named, "car suppliers price: a supplier's price is its share of the listed price, named in fifties, with a respected showroom's perk on top as the wholesaler gives it");
        // Nothing free and nothing to gain between suppliers and the trade: over three hundred mornings of the real market, every new car from every supplier, at the best
        // reputation, costs more than the trade pays for it the same day, and more than nothing.
        bool dearer = true;
        int looked = 0, least = int.MaxValue;
        var state = new DealerState { Seed = 4711 };
        for (int day = 1; day <= 300; day++)
        {
            DealerRules.Morning(state, day, SupplyModelsOf, null, 1000 + day);
            foreach (DealerListing l in state.Market)
            {
                if (l.Kind != ListingKind.Wholesale)
                {
                    continue;
                }
                int kind = CarSupplyRules.KindOf(SupplyModelsOf, l.Type), trade = DealerRules.TradePrice(DealerRules.WorthOf(l, SupplyModelsOf, 1000 + day));
                for (int s = 0; s < CarSupplyCatalogue.Suppliers; s++)
                {
                    int price = CarSupplyRules.ShowroomPrice(l.Asking, s, kind, DealerCatalogue.RepMost);
                    dearer &= price > trade && price > 0;
                    least = Math.Min(least, price - trade);
                    looked++;
                }
            }
        }
        Check(dearer && looked >= 300 * DealerCatalogue.WholesaleADay * CarSupplyCatalogue.Suppliers, "car suppliers no arbitrage: every new car costs more from any supplier than the trade pays for it (" + looked + " prices, the nearest " + least + " apart)");
        Check((long)DealerCatalogue.WholesaleLeast * CarSupplyCatalogue.PriceLeast / 1000 * (1000 - DealerCatalogue.PerkWholesale) / 1000 > DealerCatalogue.TradeShare,
            "car suppliers no arbitrage: the cheapest supplier's cheapest car at the best reputation is still dearer than the trade's share of its worth");
        Check(CarSupplyRules.Due(1000, CarSupplyCatalogue.Wholesaler) == 1000 && !CarSupplyRules.Ships(CarSupplyCatalogue.Wholesaler) && CarSupplyRules.Ships(CarSupplyCatalogue.Importer)
              && CarSupplyRules.Due(41 * 24 + 15, CarSupplyCatalogue.Importer) == (41 + CarSupplyCatalogue.Days[CarSupplyCatalogue.Importer]) * 24 + DealerCatalogue.DeliveryHour
              && CarSupplyRules.Due(41 * 24 + 15, CarSupplyCatalogue.Specialist) == (41 + CarSupplyCatalogue.Days[CarSupplyCatalogue.Specialist]) * 24 + DealerCatalogue.DeliveryHour
              && CarSupplyRules.Due(41 * 24, CarSupplyCatalogue.Importer) == CarSupplyRules.Due(41 * 24 + 23, CarSupplyCatalogue.Importer) && CarSupplyRules.Days(-1) == 0 && CarSupplyRules.Days(99) == 0,
            "car suppliers delivery: the wholesaler's car is there at once, another's so many days on at the delivery hour, the same whenever in the day it is ordered");
    }

    private static DealerYard SupplyYard(string place, int spots)
    {
        var y = new DealerYard { Place = place, Name = place };
        for (int s = 0; s < spots; s++)
        {
            y.Spots.Add(new LotSpot { X = s * 340 });
        }
        return y;
    }

    private static void SupplyPlanChecks()
    {
        var s = new DealerState { Seed = 99 };
        DealerYard a = SupplyYard("A~1", 8), b = SupplyYard("B~2", 3);
        s.Yards.Add(a);
        s.Yards.Add(b);
        int id = 1;
        foreach (int[] car in new[] { new[] { 0, 16000 }, new[] { 0, 16500 }, new[] { 0, 17000 }, new[] { 3, 120000 }, new[] { 5, 34000 }, new[] { 6, 70000 } })
        {
            s.Market.Add(new DealerListing { Id = id++, Kind = ListingKind.Wholesale, Type = SupplyModelsOf[car[0]].Type, Km = 20, Condition = 1000, Shown = 1000, Inspected = true, Asking = car[1] });
        }
        s.Market.Add(new DealerListing { Id = id++, Kind = ListingKind.Used, Type = "eco", Km = 40000, Condition = 800, Asking = 9000 });
        s.Market.Add(new DealerListing { Id = id++, Kind = ListingKind.Wholesale, Type = "eco", Km = 20, Condition = 1000, Asking = 15000, Gone = true });
        var sup = new CarSupplyState();
        var orders = new List<CarSupplyOrder>();
        Check(CarSupplyRules.Plan(s, sup, a, SupplyModelsOf, 1000000, 500, orders) == 1000000 && orders.Count == 0, "car suppliers standing order: with none set a manager orders nothing");
        sup.Supplier[0] = CarSupplyCatalogue.Importer;
        sup.KeepOf("A~1", true).Keep[0] = 2;
        sup.KeepOf("A~1", true).Keep[3] = 1;
        sup.KeepOf("B~2", true).Keep[0] = 5;
        long left = CarSupplyRules.Plan(s, sup, a, SupplyModelsOf, 1000000, 500, orders);
        Check(orders.Count == 3 && orders[0].Listing == 1 && orders[1].Listing == 2 && orders[0].Kind == 0 && orders[0].Supplier == CarSupplyCatalogue.Importer && orders[0].Ships
              && orders[0].Price == CarSupplyRules.ShowroomPrice(16000, CarSupplyCatalogue.Importer, 0, 0) && orders[0].Due == CarSupplyRules.Due(500, CarSupplyCatalogue.Importer) && orders[2].Listing == 4
              && orders[2].Supplier == CarSupplyCatalogue.Wholesaler && !orders[2].Ships && orders[2].Price == 120000 && orders[2].Due == 500,
            "car suppliers standing order: the cheapest new cars of each kind short of its number, from the kind's supplier at its price and delivery");
        Check(left == 1000000 - orders.Sum(o => (long)o.Price) && left >= 0, "car suppliers standing order: the cash left is the cash less what was ordered");
        long left2 = CarSupplyRules.Plan(s, sup, b, SupplyModelsOf, left, 500, orders);
        Check(orders.Count == 4 && orders[3].Listing == 3 && orders.Select(o => o.Listing).Distinct().Count() == orders.Count && left2 == left - orders[3].Price,
            "car suppliers standing order: the next showroom's manager takes what is left on the list, and no listing is ordered twice");
        Check(orders.All(o => !s.Listing(o.Listing).Gone && s.Listing(o.Listing).Kind == ListingKind.Wholesale && o.Kind != -1), "car suppliers standing order: only new cars the market lists today, never a used one, one gone or a kind no supplier brings");
        // The caps: the cash, the lot's room, the deliveries on their way.
        var poor = new List<CarSupplyOrder>();
        long kept = CarSupplyRules.Plan(s, sup, a, SupplyModelsOf, orders[0].Price + 10, 500, poor);
        Check(poor.Count == 1 && kept == 10 && kept >= 0, "car suppliers standing order: nothing is ordered that the cash at hand does not pay for");
        var tiny = new List<CarSupplyOrder>();
        DealerYard one = SupplyYard("C~3", 1);
        s.Yards.Add(one);
        sup.KeepOf("C~3", true).Keep[0] = 9;
        CarSupplyRules.Plan(s, sup, one, SupplyModelsOf, 1000000, 500, tiny);
        Check(tiny.Count == 1, "car suppliers standing order: never more cars than the lot has free places");
        var unlaid = new List<CarSupplyOrder>();
        sup.KeepOf("D~4", true).Keep[0] = 2;
        CarSupplyRules.Plan(s, sup, new DealerYard { Place = "D~4" }, SupplyModelsOf, 1000000, 500, unlaid);
        Check(unlaid.Count == 0, "car suppliers standing order: a showroom with no lot laid out orders nothing");
        // A showroom mid order: a car on its way counts as had and holds its place, so the same morning asked twice (a reload) orders nothing more.
        var mid = new List<CarSupplyOrder>();
        s.Deliveries.Add(new DealerDelivery { Id = 900, Place = "A~1", Type = "eco", Km = 20, Condition = 1000, Cost = 15000, Due = 600 });
        s.Deliveries.Add(new DealerDelivery { Id = 901, Place = "A~1", Type = "eco", Km = 20, Condition = 1000, Cost = 15000, Due = 600 });
        s.Deliveries.Add(new DealerDelivery { Id = 902, Place = "A~1", Type = "sport", Km = 20, Condition = 1000, Cost = 15000, Due = 600 });
        Check(CarSupplyRules.Have(s, a, SupplyModelsOf, 0) == 2 && CarSupplyRules.Have(s, a, SupplyModelsOf, 3) == 1 && CarSupplyRules.Have(s, b, SupplyModelsOf, 0) == 0
              && CarSupplyRules.Plan(s, sup, a, SupplyModelsOf, 1000000, 500, mid) == 1000000 && mid.Count == 0, "car suppliers mid order: cars on their way count towards the standing order, so they are not ordered again");
        while (s.Deliveries.Count < DealerCatalogue.DeliveriesMost)
        {
            s.Deliveries.Add(new DealerDelivery { Id = 1000 + s.Deliveries.Count, Place = "Z~9", Type = "van", Cost = 1, Due = 600 });
        }
        var blocked = new List<CarSupplyOrder>();
        CarSupplyRules.Plan(s, sup, b, SupplyModelsOf, 1000000, 500, blocked);
        Check(blocked.Count == 0, "car suppliers standing order: with every delivery taken, a supplier that sends its cars is not ordered from");
        sup.Supplier[0] = CarSupplyCatalogue.Wholesaler;
        CarSupplyRules.Plan(s, sup, b, SupplyModelsOf, 1000000, 500, blocked);
        Check(blocked.Count == 3 && blocked.All(o => !o.Ships), "car suppliers standing order: the wholesaler's cars need no delivery and still come");
        Check(CarSupplyRules.Keep(sup, "A~1", 0) == 2 && CarSupplyRules.Keep(sup, "nowhere", 0) == 0 && CarSupplyRules.Keep(sup, "A~1", 9) == 0 && CarSupplyRules.Keep(null, "A~1", 0) == 0, "car suppliers standing order: a number is a showroom's and a kind's own");
        string dir = ModSource(@"Economy\Dealer\Supply\DealerDirector.Supply.cs");
        Check(dir.Contains("if (!SupplyOn || _state.MarketDay != day)") && dir.Contains("SupplyOrders(day, now, _supplyDecides)") && dir.Contains("private bool SupplyDecides(DealerYard y) => RegOf(y) != null && SupplyManaged(y);"),
            "car suppliers standing order: asked each hour of the day's market, for the showrooms of the player whose manager decides");
    }

    private static void SupplyRivalChecks()
    {
        bool range = true, keen = true, quick = true;
        int cheapest = 0, wholesaler = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            for (int r = 0; r < DealerCatalogue.RivalNames.Length; r++)
            {
                int bias = RivalRules.BiasOf(seed, r);
                for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
                {
                    int s = CarSupplyRules.RivalSupplier(seed, r, k);
                    range &= s >= 0 && s < CarSupplyCatalogue.Suppliers && s == CarSupplyRules.RivalSupplier(seed, r, k) && (s == CarSupplyCatalogue.Wholesaler || s == CarSupplyRules.Cheapest(k));
                    keen &= bias >= CarSupplyCatalogue.RivalKeen || s == CarSupplyRules.Cheapest(k);
                    quick &= bias < CarSupplyCatalogue.RivalQuick || s == CarSupplyCatalogue.Wholesaler;
                    cheapest += s != CarSupplyCatalogue.Wholesaler ? 1 : 0;
                    wholesaler += s == CarSupplyCatalogue.Wholesaler ? 1 : 0;
                }
            }
        }
        Check(range && keen && quick && cheapest > 0 && wholesaler > 0, "car suppliers other dealers: a keen dealer buys each kind from the cheapest, a dear one from the wholesaler, the same all game ("
            + cheapest + " and " + wholesaler + ")");
        Check(CarSupplyRules.RivalSupplier(5, 2, -1) == CarSupplyCatalogue.Wholesaler && CarSupplyRules.RivalSupplier(5, 2, 9) == CarSupplyCatalogue.Wholesaler, "car suppliers other dealers: a kind unknown comes from the wholesaler");
        int ask = 20000, day = 50, askOff = 20000, dayOff = 50;
        int seedKeen = Enumerable.Range(1, 500).First(x => RivalRules.BiasOf(x, 0) < CarSupplyCatalogue.RivalKeen);
        CarSupplyRules.RivalBuys(ref ask, ref day, seedKeen, 0, (int)CarKind.Economy, true);
        CarSupplyRules.RivalBuys(ref askOff, ref dayOff, seedKeen, 0, (int)CarKind.Economy, false);
        Check(ask == CarSupplyRules.Price(20000, CarSupplyRules.Cheapest(0), 0) && ask < 20000 && day == 50 + CarSupplyCatalogue.Days[CarSupplyRules.Cheapest(0)] && askOff == 20000 && dayOff == 50,
            "car suppliers other dealers: a dealer's car costs it what its supplier asks and comes when its supplier brings it; with the option off, as before");
        int truckAsk = 20000, truckDay = 50;
        CarSupplyRules.RivalBuys(ref truckAsk, ref truckDay, seedKeen, 0, (int)CarKind.Truck, true);
        Check(truckAsk == 20000 && truckDay == 50, "car suppliers other dealers: a kind no supplier brings is as before");
        // The morning with and without: the same cars, and with suppliers some are still on their way and are not yet on the lot for a buyer to compare.
        var with = new DealerState { Seed = seedKeen };
        var without = new DealerState { Seed = seedKeen };
        DealerDay.Morning(with, 10, SupplyModelsOf, null, 1000, true, true);
        DealerDay.Morning(without, 10, SupplyModelsOf, null, 1000);
        bool same = with.Rivals.Count == without.Rivals.Count && with.Rivals.Count > 0, late = false, before = true;
        for (int i = 0; same && i < with.Rivals.Count; i++)
        {
            same = with.Rivals[i].Type == without.Rivals[i].Type && with.Rivals[i].Rival == without.Rivals[i].Rival;
            late |= with.Rivals[i].Day > 10;
            before &= without.Rivals[i].Day == 10;
        }
        Check(same && late && before, "car suppliers other dealers: the morning's restock is the same cars, some of them days out with their supplier; without the option none is");
        RivalCar waiting = with.Rivals.First(c => c.Day > 10);
        var lot = new DealerState { Seed = 3, MarketDay = 10 };
        lot.Rivals.Add(new RivalCar { Rival = 1, Type = "eco", Ask = 15000, Day = 12 });
        lot.Rivals.Add(new RivalCar { Rival = 2, Type = "eco", Ask = 17000, Day = 9 });
        int cheap = RivalRules.Cheapest(lot, "eco", out int who);
        lot.MarketDay = 12;
        int later = RivalRules.Cheapest(lot, "eco", out int then);
        lot.MarketDay = -1;
        Check(cheap == 17000 && who == 2 && later == 15000 && then == 1 && RivalRules.Cheapest(lot, "eco", out int _) == 15000 && waiting.Day <= 10 + DealerCatalogue.DeliveryDaysMost,
            "car suppliers other dealers: a car still on its way is not compared with the player's until it has come");
    }

    private static void SupplyStoreChecks()
    {
        Check(CarSupplyStore.Load(new Dictionary<string, string>(), out int v0) == null && v0 == 0 && CarSupplyStore.Read(string.Empty, out int _) == null && CarSupplyStore.Read("nothing of ours", out int _) == null,
            "car suppliers save: a save from before has no line, and every kind comes from the wholesaler");
        var s = new CarSupplyState();
        s.Supplier[0] = 1;
        s.Supplier[3] = 2;
        s.KeepOf("5th%20Avenue~12", true).Keep[0] = 3;
        s.KeepOf("5th%20Avenue~12", true).Keep[5] = 12;
        s.KeepOf("B;road,way^x~7", true).Keep[2] = 1;
        s.KeepOf("5th%20Avenue~12", true).Day = 311;
        s.Ordered(57, 1);
        s.Ordered(58, 2);
        s.Ordered(57, 2);
        string line = CarSupplyStore.Write(s);
        CarSupplyState r = CarSupplyStore.Read(line, out int version);
        Check(version == CarSupplyStore.Version && r != null && CarSupplyStore.Write(r) == line && r.Supplier[0] == 1 && r.Supplier[3] == 2 && r.Supplier[1] == 0 && r.KeepOf("5th%20Avenue~12", false).Day == 311 && r.KeepOf("B;road,way^x~7", false).Day == -1 && r.Keeps.Count == 2
              && r.KeepOf("5th%20Avenue~12", false).Keep[5] == 12 && r.KeepOf("B;road,way^x~7", false).Keep[2] == 1 && r.Orders.Count == 2 && r.OrderOf(57) == 1 && r.OrderOf(58) == 2 && r.OrderOf(59) == -1,
            "car suppliers save: a line written, read and written again is the same line, with every field and a place of any spelling");
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Check(CarSupplyStore.Write(s) == line && CarSupplyStore.Write(CarSupplyStore.Read(line, out int _)) == line, "car suppliers save: a language with a decimal comma writes and reads the same line");
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
        CarSupplyState newer = CarSupplyStore.Read("v=7^s:2,1,0,0,0,1,2,2^d:40,9^k:A~1,1,2,3,4,5,6,7,8^o:5=1=9;6=2^q:new", out int seven);
        Check(seven == 7 && newer != null && newer.Supplier[0] == 2 && newer.Supplier[5] == 1 && newer.KeepOf("A~1", false).Day == 7 && newer.KeepOf("A~1", false).Keep[5] == 6 && newer.OrderOf(5) == 1 && newer.OrderOf(6) == 2,
            "car suppliers save: a line from a newer version is read as far as it goes");
        CarSupplyState hurt = CarSupplyStore.Read("v=1^s:9,-4,x^d:y^k:A~1,99,-3;,5^o:0=1;x=2;7=44", out int _);
        Check(hurt != null && hurt.Supplier[0] == CarSupplyCatalogue.Suppliers - 1 && hurt.Supplier[1] == 0 && hurt.Supplier[2] == 0 && hurt.KeepOf("A~1", false).Day == -1 && hurt.KeepOf("A~1", false).Keep[0] == CarSupplyCatalogue.KeepMost
              && hurt.KeepOf("A~1", false).Keep[1] == 0 && hurt.Orders.Count == 1 && hurt.OrderOf(7) == CarSupplyCatalogue.Suppliers - 1, "car suppliers save: a damaged line keeps what can be read, every number inside its bounds");
        // A showroom mid order: the mark of a supplier's delivery outlives a save and a load, goes when the car has come, and never outlives its delivery.
        var deals = new DealerState();
        deals.Deliveries.Add(new DealerDelivery { Id = 57, Place = "A~1", Type = "eco", Due = 900 });
        CarSupplyRules.Prune(r, deals);
        Check(r.Orders.Count == 1 && r.OrderOf(57) == 1 && r.OrderOf(58) == -1, "car suppliers mid order: a mark whose delivery is gone is let go, one whose car is on its way is kept");
        r.Arrived(57);
        Check(r.Orders.Count == 0 && r.OrderFrom.Count == 0 && r.OrderOf(57) == -1, "car suppliers mid order: a car that has come is no longer on order");
        string dir = ModSource(@"Economy\Dealer\DealerDirector.Business.cs");
        Check(dir.Contains("Arrive(y, q.Type, q.Colour, q.Km, q.Condition, q.Faults, q.Known, q.Cost, DeliverySource(q), true)") && dir.Contains("_supply.Arrived(q.Id);")
              && ModSource(@"Economy\Dealer\Supply\DealerDirector.Supply.cs").Contains("return CarSupplyRules.SourceOf(_supply, q.Id);"),
            "car suppliers mid order: a car on order arrives as a new car on the terms it was ordered on, whatever the option says by then");
    }

    private static void SupplyPageChecks()
    {
        UiWords en = ModEnglish();
        UiSamples.SupplyPressed press = UiSamples.SupplyPress;
        EstateModel m = UiSamples.Dealer(en, "suppliers");
        Check(m.Shape == EstateShape.Detail && m.Left.Length == 1 && m.Right.Length == 2 && m.Strip.Length == 4 && m.Left[0].Lines.Count(l => l.Kind == UiFactKind.Step) == CarSupplyCatalogue.Kinds,
            "car suppliers page: a step for each kind of car, the standing order and where the other dealers buy");
        UiFactLine[] steps = m.Left[0].Lines.Where(l => l.Kind == UiFactKind.Step).ToArray();
        Check(steps[0].Value == SupplyWords.Name(en, CarSupplyCatalogue.Importer) && steps[0].Hint == SupplyWords.Terms(en, CarSupplyCatalogue.Importer, 0) && steps[1].Value == SupplyWords.Name(en, CarSupplyCatalogue.Wholesaler)
              && !steps[1].CanLess && steps[1].CanMore && steps[3].CanLess && !steps[3].CanMore, "car suppliers page: each kind shows its supplier with its price and delivery, and steps only as far as there are suppliers");
        m.Runs[steps[1].Arg + 1]();
        bool more = press.Last == "supplier" && press.Kind == 1 && press.Value == 1;
        m.Runs[steps[0].Arg]();
        Check(more && press.Last == "supplier" && press.Kind == 0 && press.Value == 0, "car suppliers page: a step of a kind reaches the call that sets its supplier");
        UiFactLine[] keeps = m.Right[0].Lines.Where(l => l.Kind == UiFactKind.Step).ToArray();
        UiFactLine eco = keeps[keeps.Length - CarSupplyCatalogue.Kinds];
        m.Runs[eco.Arg + 1]();
        bool keepMore = press.Last == "keep" && press.Kind == 0 && press.Value == 4 && eco.Value == "3";
        m.Runs[eco.Arg]();
        Check(keepMore && press.Last == "keep" && press.Value == 2 && press.Place.Length > 0, "car suppliers page: a step of the standing order reaches the call that sets it, one more or one fewer");
        if (keeps.Length > CarSupplyCatalogue.Kinds)
        {
            m.Runs[keeps[0].Arg + 1]();
            Check(press.Last == "yard" && press.Place.Length > 0, "car suppliers page: with several showrooms the step among them reaches its call");
        }
        Check(m.Right[1].Lines.Length == CarSupplyCatalogue.Kinds && m.Right[1].Lines.All(l => l.Value.Contains("(")), "car suppliers page: where the other dealers buy each kind, by name and number");
        Check(SupplyWords.Against(en, 0, 0) != SupplyWords.Against(en, 1, 0) && SupplyWords.Against(en, 1, 0).Contains("3.5") && SupplyWords.Against(en, 1, 3).Contains("1.5") && SupplyWords.Against(en, 1, 0) != SupplyWords.Against(en, 1, 3)
              && SupplyWords.Delivery(en, 0) != SupplyWords.Delivery(en, 1) && SupplyWords.Delivery(en, 1).Contains("3"), "car suppliers page: a price under and over the wholesaler's and a delivery read as the table has them");
        var lines = new List<UiFactLine>();
        CarSupplyPages.NewLines(lines, en, CarSupplyCatalogue.Specialist);
        Check(lines.Count == 2 && lines[0].Value == SupplyWords.Name(en, CarSupplyCatalogue.Specialist) && lines[1].Value == SupplyWords.Delivery(en, CarSupplyCatalogue.Specialist), "car suppliers page: a new car's page says who supplies it and when it comes");
        string app = ModSource(@"Economy\Dealer\Supply\DealerApp.Supply.cs"), live = ModSource(@"Ui\Estate\DealerLive.Supply.cs");
        Check(app.Contains("_director.SetSupplier(kind, to)") && app.Contains("_director.SetKeep(place, kind, keep + 1)") && app.Contains("_director.SetKeep(place, kind, keep - 1)") && live.Contains("_d.SetSupplier(kind, supplier)")
              && live.Contains("_d.SetKeep(place, kind, count)"), "car suppliers page: the app in the game's frame and the kit's page press the same calls of the director");
        string market = ModSource(@"Economy\Dealer\DealerApp.Market.cs"), kit = ModSource(@"Ui\Estate\DealerLive.Market.cs"), actions = ModSource(@"Economy\Dealer\DealerDirector.Actions.cs");
        Check(market.Contains("int price = _director.NewPrice(l, to);") && kit.Contains("int price = _d.NewPrice(l, to);") && actions.Contains("price = NewPrice(l, y);") && market.Contains("_director.NewQuestion(l, price)")
              && kit.Contains("string question = _d.NewQuestion(l, price);"), "car suppliers page: the price a page shows is the price the purchase charges, and the question says when a car on order comes");
    }

    private static void SupplySourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Economy\Dealer\Supply\CarSupplyRules.cs"));
        string[] files = Directory.GetFiles(folder, "*.cs").Concat(new[] { ModPath(@"Ui\Estate\DealerLive.Supply.cs"), ModPath(@"Ui\Estate\CarSupplyPages.cs") }).ToArray();   // the page stands in the folder of the screen it is part of
        Check(files.Length >= 9, "car suppliers sources: the feature's files are found (" + files.Length + ")");
        foreach (string file in files)
        {
            string text = File.ReadAllText(file), name = System.IO.Path.GetFileName(file);
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("BizBooks.") && !text.Contains("Update()") && !text.Contains("FindObjectsByType") && !text.Contains("UnityEngine.Random"),
                "car suppliers sources: " + name + " books no money of its own, draws nothing at the moment of ordering and has no per-frame code or scene search");
            Check(!text.Contains("TODO") && !text.Contains("NotImplemented") && text.Split('\n').All(l => l.TrimEnd('\r').Length < 200), "car suppliers sources: " + name + " has no stub, and no line of 200 columns");
        }
        string dir = ModSource(@"Economy\Dealer\Supply\DealerDirector.Supply.cs"), actions = ModSource(@"Economy\Dealer\DealerDirector.Actions.cs");
        int shipped = dir.IndexOf("private bool BuyShipped(", StringComparison.Ordinal);
        int waysFull = shipped > 0 ? dir.IndexOf("CarSupplyRules.WaysFull(_state)", shipped, StringComparison.Ordinal) : -1;
        int taken = shipped > 0 ? dir.IndexOf("DealerGame.Take(price", shipped, StringComparison.Ordinal) : -1, sent = shipped > 0 ? dir.IndexOf("_state.Deliveries.Add(q);", shipped, StringComparison.Ordinal) : -1;
        Check(shipped > 0 && waysFull > shipped && taken > waysFull && sent > taken
              && CountOfText(dir, "DealerGame.Take(") == 1 && !dir.Contains("DealerGame.Give("), "car suppliers sources: an order is refused before any money when no delivery can be taken, paid for once, and only then sent; nothing is ever given back or cancelled");
        Check(actions.Contains("return BuyShipped(l, y, reg, price, supplier);") && actions.IndexOf("DealerYard y = Room(yardPlace, out DealerRefusal why);", StringComparison.Ordinal) < actions.IndexOf("return BuyShipped(", StringComparison.Ordinal),
            "car suppliers sources: a car on order goes through the purchase path, after the lot's room is found");
        Check(dir.Contains("internal bool SupplyOn => _running && Settings.EconomyDealerSupply;") && dir.Contains("CarSupplyRules.SupplierFor(_supply, SupplyKind(l), SupplyOn)") && dir.Contains("if (!SupplyOn || kind < 0"),
            "car suppliers off: with the option off every new car is the wholesaler's, and no supplier or standing order is set");
        Check(ModSource(@"Economy\Dealer\DealerDirector.Clock.cs").Contains("DealerDay.Morning(_state, day, _models, _colours, level, true, SupplyOn);") && ModSource(@"Economy\Dealer\DealerDirector.Clock.cs").Contains("SupplyMorning(day, now);")
              && ModSource(@"Economy\Dealer\RivalRules.cs").Contains("CarSupplyRules.RivalBuys(ref ask, ref out_, s.Seed, r, m.Kind, suppliers);") && ModSource(@"Economy\Dealer\RivalRules.cs").Contains("(s.MarketDay < 0 || c.Day <= s.MarketDay)"),
            "car suppliers sources: the morning gives the other dealers their suppliers and the managers their orders");
        Check(ModSource(@"Economy\Dealer\DealerDirector.cs").Contains("SupplyLoad(save);") && ModSource(@"Economy\Dealer\DealerDirector.cs").Contains("SupplyWrite(save);") && ModSource(@"Economy\Dealer\DealerDirector.cs").Contains("SupplyForget();"),
            "car suppliers sources: the line is read with the dealership's, written with it and forgotten with it");
        Check(ModSource(@"Wiring\Wiring.BizAsksLinks.cs").Contains("company.Decides(place, ManagerTask.Stock)") && ModSource(@"Wiring\Wiring.BizAsksLinks.cs").Contains("CarSupplyBridges.Manages = Manages;"), "car suppliers sources: the manager is the company's own, with its Stock orders task");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("Economy.Dealership.CarSupplyGuide.Register()") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"carsupply\")") && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"economy_dealer_supply\"")
              && ModSource(@"Core\Settings.cs").Contains("public static bool EconomyDealerSupply = true;") && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("DealerDirector.Instance?.TestSupplyOrder()")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("DealerDirector.Instance?.TestSupplyManager()"), "car suppliers sources: the feature has its option (on), its Guide page, its place in the cities list and its tests");
    }

    // ---- the texts of both

    private static void BizAsksTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string[] sources =
        {
            @"ParkSpaces\SpaceDirector.cs", @"ParkSpaces\SpaceDirector.Tests.cs", @"ParkSpaces\SpaceGame.cs", @"ParkSpaces\SpaceText.cs", @"Ui\Estate\SpacePages.cs", @"Economy\Estate\EstateApp.Spaces.cs",
            @"Ui\Estate\EstateLive.Spaces.cs", @"Economy\Dealer\Supply\CarSupplyCatalogue.cs", @"Economy\Dealer\Supply\CarSupplyText.cs", @"Ui\Estate\CarSupplyPages.cs",
            @"Economy\Dealer\Supply\DealerApp.Supply.cs", @"Ui\Estate\DealerLive.Supply.cs", @"Economy\Dealer\Supply\DealerDirector.Supply.cs"
        };
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(ModSource(source), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string k in new[] { "reallife_space_enabled", "reallife_opt_sec_parkspaces", "reallife_opt_tip_parking_spaces", "reallife_carsup_enabled", "reallife_opt_sec_carsupply", "reallife_opt_tip_economy_dealer_supply",
            "reallife_carsup_note_arrived", "reallife_carsup_note_no_place" })
        {
            keys.Add(k);
        }
        Check(keys.Count >= 130, "smaller business asks texts: their keys are found: " + keys.Count);
        string[] spaceFacts = { "{space_fee}", "{space_rent_days}", "{space_rent}", "{space_back_fee}", "{space_held_most}", "{space_park_most}", "{space_gone_days}" };
        string[] supplyFacts = { "{carsup_importer_days}", "{carsup_specialist_days}", "{carsup_importer_economy}", "{carsup_importer_saloon}", "{carsup_importer_van}", "{carsup_importer_dear}", "{carsup_specialist_luxury}",
            "{carsup_specialist_sports}", "{carsup_specialist_utility}", "{carsup_specialist_dear}", "{carsup_keep_most}", "{carsup_hour}", "{carsup_ways_most}" };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(english != null, "smaller business asks texts: en.json is read");
        // What's new: the row that holds the English line is found by the line, whatever its key has become, and read in every language.
        const string sentence = "You can now buy or rent a single parking space in a building's car park from the Property app, and choose a supplier for each kind of car at your Car Showroom";
        string row = english == null ? null : english.Keys.FirstOrDefault(k => k.StartsWith("reallife_changes_", StringComparison.Ordinal) && !k.EndsWith("_title", StringComparison.Ordinal) && english[k].Contains(sentence));
        Check(row != null, "smaller business asks texts: What's new has the feature's line in English");
        if (row != null && row == "reallife_changes_next")
        {
            int at = english[row].IndexOf(sentence, StringComparison.Ordinal), mark = english[row].IndexOf("Fixed: ", StringComparison.Ordinal);
            Check(mark < 0 || at < mark, "smaller business asks texts: the feature's line stands before the first fix mark");
        }
        foreach (string code in BizAsksLanguages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "smaller business asks texts: " + code + ".json is read");
            if (words == null || english == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "smaller business asks texts: " + key + " is in " + code + ".json");
                if (!there || !english.TryGetValue(key, out string en))
                {
                    continue;
                }
                Check(!value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - "), "smaller business asks texts: " + key + " has no exclamation mark or dash in " + code);
                var wanted = new SortedSet<string>(Regex.Matches(en, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                var got = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                Check(wanted.SetEquals(got), "smaller business asks texts: " + key + " keeps its {names} in " + code);
                Check(Regex.Matches(en, "<b>").Count == Regex.Matches(value, "<b>").Count && Regex.Matches(en, @"\n").Count == Regex.Matches(value, @"\n").Count, "smaller business asks texts: " + key + " keeps its tags and lines in " + code);
                if (key.Contains("_note_") || key.Contains("_refuse_") || key.Contains("_test_") || key.Contains("_q_"))
                {
                    Check(value.Length <= 240, "smaller business asks texts: the notice " + key + " is at most 240 characters in " + code + " (" + value.Length + ")");
                }
            }
            if (words.TryGetValue("reallife_guide_parking_spaces", out string spacePage))
            {
                Check(spaceFacts.All(spacePage.Contains), "smaller business asks texts: the parking spaces' Guide page has its facts in " + code);
            }
            if (words.TryGetValue("reallife_guide_economy_dealer_supply", out string supplyPage))
            {
                Check(supplyFacts.All(supplyPage.Contains), "smaller business asks texts: the car suppliers' Guide page has its facts in " + code);
            }
            if (row != null)
            {
                bool said = words.TryGetValue(row, out string theirs) && theirs.Length > 60 && (code == "en" || (!theirs.Contains(sentence) && theirs != english[row]));
                Check(said, "smaller business asks texts: What's new has the feature's line, in its own words, in " + code);
            }
        }
        string spaceGuide = ModSource(@"ParkSpaces\SpaceText.cs"), supplyGuide = ModSource(@"Economy\Dealer\Supply\CarSupplyText.cs");
        Check(spaceFacts.All(f => spaceGuide.Contains(".Fact(\"" + f.Trim('{', '}') + "\"")) && supplyFacts.All(f => supplyGuide.Contains(".Fact(\"" + f.Trim('{', '}') + "\"")), "smaller business asks texts: every fact of the two Guide pages is filled from the rules");
        if (english != null)
        {
            Check(english["reallife_guide_parking_spaces"].Contains("<b>" + english["reallife_space_enabled"] + "</b>") && english["reallife_guide_parking_spaces"].Contains("<b>" + english["reallife_space_btn_buy"] + "</b>")
                  && english["reallife_guide_parking_spaces"].Contains("<b>" + english["reallife_space_btn_rent"] + "</b>") && english["reallife_guide_parking_spaces"].Contains("<b>" + english["reallife_space_btn_sell"] + "</b>")
                  && english["reallife_guide_parking_spaces"].Contains("<b>" + english["reallife_space_btn_end"] + "</b>"), "smaller business asks texts: the parking spaces' Guide page names its option and buttons as the game shows them");
            Check(english["reallife_guide_economy_dealer_supply"].Contains("<b>" + english["reallife_carsup_enabled"] + "</b>") && english["reallife_guide_economy_dealer_supply"].Contains("<b>" + english["reallife_carsup_link"] + "</b>")
                  && Enumerable.Range(0, 3).All(i => english["reallife_guide_economy_dealer_supply"].Contains("<b>" + english["reallife_carsup_name_" + i] + "</b>"))
                  && english["reallife_guide_economy_dealer_supply"].Contains("<b>" + english["reallife_mgr_task_stock"] + "</b>"), "smaller business asks texts: the car suppliers' Guide page names its option, its link, the suppliers and the manager's task as the game shows them");
        }
    }
}
