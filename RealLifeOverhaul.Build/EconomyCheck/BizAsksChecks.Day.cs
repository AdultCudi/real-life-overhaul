using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.ParkSpaces;

// The smaller business asks, the day and the hour (run with --bizasks): the checks that drive the very rules the directors call, with a ledger of their own in the place of the
// game. The hour counted once (a save and a load in the middle, a time skip, the option off and on), the game's charge paid back only for what the game took, the day's pass in
// a shared city and out of one, a car park that is gone or smaller, a building that becomes the player's, the rent, the managers' morning, and the other dealers' cars on their way.
internal static partial class Program
{
    // What the game would be asked and told by the day's pass, written down: the player's cash, each line booked, each notice, each tycoon's worth moved.
    private sealed class BizLedger : ISpaceLedger
    {
        public long Cash;
        public readonly Dictionary<SpaceLine, long> Got = new Dictionary<SpaceLine, long>();
        public readonly Dictionary<SpaceLine, long> Took = new Dictionary<SpaceLine, long>();
        public readonly List<SpaceNote> Notes = new List<SpaceNote>();
        public readonly List<KeyValuePair<int, long>> Tycoons = new List<KeyValuePair<int, long>>();

        public bool Pay(long amount, SpaceLine line, SpacePark park)
        {
            if (amount > Cash)
            {
                return false;
            }
            Cash -= amount;
            Took[line] = Took.TryGetValue(line, out long n) ? n + amount : amount;
            return true;
        }

        public void Receive(long amount, SpaceLine line, SpacePark park, SpaceHolding h)
        {
            Cash += amount;
            Got[line] = Got.TryGetValue(line, out long n) ? n + amount : amount;
        }

        public void Tell(SpaceNote note, SpacePark park, SpaceHolding h, long amount)
        {
            Notes.Add(note);
        }

        public void Tycoon(int who, long worth)
        {
            Tycoons.Add(new KeyValuePair<int, long>(who, worth));
        }

        public long GotOf(SpaceLine line) => Got.TryGetValue(line, out long n) ? n : 0L;

        public long TookOf(SpaceLine line) => Took.TryGetValue(line, out long n) ? n : 0L;
    }

    private static void SpaceDayChecks()
    {
        SpaceCountedChecks();
        SpaceChargedChecks();
        SpaceRentEndChecks();
        SpaceEndingChecks();
        SpaceSessionChecks();
        SpaceShrunkChecks();
    }

    // ---- the hour: counted once, from the game's own event, for the vehicles the game is about to charge

    private static void SpaceCountedChecks()
    {
        SpacePark park = BizPark(2, 12, 0);
        var parks = new List<SpacePark> { park, BizPark(8, 10, 0, SpaceOwner.City) };
        var s = new SpaceState { Seed = 12 };
        SpaceRules.Take(s, park, 1, SpaceHolding.PlayerIs, 3, park.Price, 0);
        var standing = new Dictionary<int, List<string>> { { 2, new List<string> { "car-a", "car-b" } }, { 8, new List<string> { "car-c" } } };
        Action<int, List<string>> cars = (n, into) =>
        {
            if (standing.TryGetValue(n, out List<string> list))
            {
                into.AddRange(list);
            }
        };
        var scratch = new List<string>();
        SpaceState loaded = null;
        bool once = true, repeated = true, same = true;
        int counted = 0;
        for (int hour = 0; hour < 24; hour++)
        {
            int now = 5 * 24 + hour;
            int first = SpaceDay.Hour(s, parks, now, 5, cars, scratch);
            once &= first == 1;
            counted += first;
            repeated &= SpaceDay.Hour(s, parks, now, 5, cars, scratch) == -1 && SpaceDay.Hour(s, parks, now, 5, cars, scratch) == -1;   // the event raised again by anyone
            if (hour == 12)
            {
                loaded = SpaceStore.Read(SpaceStore.Write(s), out int _);   // a save and a load in the middle of the day
                repeated &= SpaceDay.Hour(loaded, parks, now, 5, cars, scratch) == -1;   // the hour in hand is not counted again after the load
            }
            else if (loaded != null)
            {
                same &= SpaceDay.Hour(loaded, parks, now, 5, cars, scratch) == first;
            }
        }
        Check(once && repeated && counted == 24 && s.Hours(2) == 24 && s.Hours(8) == 0 && s.Pending.Count == 1,
            "parking spaces the hour: one covered car-hour an hour for the one space, however often the game's event is raised, and none in a car park the player has no space in");
        Check(same && loaded != null && loaded.Hours(2) == 24 && loaded.Pending.Count == 1 && loaded.Pending[0].Vehicle == s.Pending[0].Vehicle,
            "parking spaces the hour: a save and a load in the middle of the day changes no hour counted (24 either way) and no vehicle");
        // A long time skip: the game raises its event for each hour it passes; hours it never told the feature about are not counted, and one event counts one hour.
        long before = s.Hours(0);
        int skipped = SpaceDay.Hour(s, parks, 6 * 24 + 20, 6, cars, scratch);
        Check(skipped == 1 && s.Hours(0) == before + 1, "parking spaces the hour: after a long skip the first event counts one hour, never the hours skipped over");
        // The option off and on: hours that went by without the feature are never counted.
        before = s.Hours(0);
        SpaceDay.Resume(s, 9 * 24 + 3);
        int sameHour = SpaceDay.Hour(s, parks, 9 * 24 + 3, 9, cars, scratch), next = SpaceDay.Hour(s, parks, 9 * 24 + 4, 9, cars, scratch);
        Check(sameHour == -1 && next == 1 && s.Hours(0) == before + 1, "parking spaces the hour: switched on again (or a save loaded), the hours that passed meanwhile are not counted, and the next one is");
        // A car not in a space it is entitled to, and a driven or carried one, is never in the list: more cars than spaces cover only as many.
        var many = new Dictionary<int, List<string>> { { 2, new List<string> { "v1", "v2", "v3", "v4" } } };
        var sMany = new SpaceState { Seed = 12 };
        SpaceRules.Take(sMany, park, 1, SpaceHolding.PlayerIs, 3, park.Price, 0);
        SpaceRules.Take(sMany, park, 2, SpaceHolding.PlayerIs, 3, park.Price, 0);
        int three = SpaceDay.Hour(sMany, parks, 100, 4, (n, into) => into.AddRange(many.ContainsKey(n) ? many[n] : new List<string>()), scratch);
        Check(three == 2 && sMany.Pending.Count == 2 && sMany.Pending.All(h => h.Hours == 1), "parking spaces the hour: two spaces cover two of four cars, a car-hour each");
        // The car parks are read again when the hour finds none, or one the player holds a space in.
        var held = new SpaceState { Seed = 1 };
        SpaceRules.Take(held, park, 1, SpaceHolding.PlayerIs, 3, park.Price, 0);
        Check(SpaceDay.Stale(held, new List<SpacePark>()) && SpaceDay.Stale(held, null) && SpaceDay.Stale(held, new List<SpacePark> { BizPark(8, 10, 0) }) && !SpaceDay.Stale(held, parks)
              && !SpaceDay.Stale(new SpaceState(), parks), "parking spaces the hour: the city is read again when no car park is known, or one the player holds a space in is missing, and not otherwise");
    }

    // ---- what is paid back is what the game charged

    private static void SpaceChargedChecks()
    {
        SpacePark park = BizPark(2, 12, 0);
        var parks = new List<SpacePark> { park };
        var alive = new HashSet<string> { "car-a", "car-b" };
        Func<string, bool> there = id => alive.Contains(id);
        var s = new SpaceState { Seed = 12 };
        SpaceRules.Take(s, park, 1, SpaceHolding.PlayerIs, 3, park.Price, 0);
        SpaceRules.Take(s, park, 2, SpaceHolding.PlayerIs, 3, park.Price, 0);
        var cars = new List<string> { "car-a", "car-b" };
        var scratch = new List<string>();
        Action<int, List<string>> inPark = (n, into) => into.AddRange(cars);
        for (int hour = 0; hour < 24; hour++)
        {
            SpaceDay.Hour(s, parks, 5 * 24 + hour, 5, inPark, scratch);
        }
        var book = new BizLedger { Cash = 1000000 };
        // The midnight: the game's event (hour 0 of day 6) is counted, then the game takes day 5's charges, then the day's pass runs.
        SpaceDay.Hour(s, parks, 6 * 24, 6, inPark, scratch);
        SpaceDayResult day5 = SpaceDay.Pass(s, parks, 6, false, there, book);
        Check(day5.Refund == 2 * 24L * SpaceCatalogue.HourFee && book.GotOf(SpaceLine.Refund) == day5.Refund && s.Hours(2) == 2, "parking spaces charged: the day's pass pays back the 48 car-hours the game took the morning after, and not the first hour of the new day, which it has not charged yet");
        SpaceDayResult again = SpaceDay.Pass(s, parks, 6, false, there, book);
        Check(again.Refund == 0 && book.GotOf(SpaceLine.Refund) == day5.Refund, "parking spaces charged: the day's pass again (the test button) pays nothing more");
        // Day 6's hours are charged on the morning of day 7; the new day's car-hours of both cars were counted once each.
        for (int hour = 1; hour < 24; hour++)
        {
            SpaceDay.Hour(s, parks, 6 * 24 + hour, 6, inPark, scratch);
        }
        alive.Remove("car-b");   // sold on day 6, before the game took its charges
        SpaceDayResult day6 = SpaceDay.Pass(s, parks, 7, false, there, book);
        Check(day6.Refund == 24L * SpaceCatalogue.HourFee && s.Pending.Count == 0 && s.RefundedTotal == day5.Refund + day6.Refund,
            "parking spaces charged: a vehicle sold before the morning was never charged, so only the other car's 24 hours are paid back, and nothing is left to pay twice");
        long due = SpaceRules.RefundDue(s, 8, there);
        Check(due == 0 && SpaceRules.Refund(s, 8, there) == 0 && SpaceRules.RefundDue(new SpaceState(), 8, null) == 0, "parking spaces charged: with nothing counted nothing is owed, and a missing list of vehicles pays nothing");
        // The pending hours are bounded: a vehicle is charged at most a day of hours, however often it is counted.
        var big = new SpaceState();
        for (int i = 0; i < 100; i++)
        {
            SpaceRules.AddHour(big, 4, 2, "v");
        }
        for (int i = 0; i < SpaceCatalogue.PendingMost + 50; i++)
        {
            SpaceRules.AddHour(big, 4, 2, "w" + i);
        }
        Check(big.Pending.Count == SpaceCatalogue.PendingMost && big.Pending[0].Hours == SpaceCatalogue.HoursMost && SpaceRules.RefundDue(big, 5, id => true) <= (long)SpaceCatalogue.PendingMost * SpaceCatalogue.HoursMost * SpaceCatalogue.HourFee,
            "parking spaces charged: a vehicle is paid back for a day of hours at most, and the list of hours counted is bounded");
        // The test button: the day's pass without an hour of its own; here the same rules, run twice in the same day, make no money.
        var test = new SpaceState { Seed = 3, LastDay = 9 };
        SpaceRules.Take(test, park, 1, SpaceHolding.PlayerIs, 3, park.Price, 0);
        for (int hour = 0; hour < 24; hour++)
        {
            SpaceDay.Hour(test, parks, 8 * 24 + hour, 8, (n, into) => into.Add("car-a"), scratch);
        }
        var cash = new BizLedger { Cash = 500 };
        SpaceDay.Pass(test, parks, 9, false, there, cash);
        long first = cash.Cash;
        SpaceDay.Pass(test, parks, 9, false, there, cash);
        SpaceDay.Pass(test, parks, 9, false, there, cash);
        Check(first == 500 + 24L * SpaceCatalogue.HourFee && cash.Cash == first, "parking spaces the test button: pressed again and again in a day it pays back what the game took once, and never anything of its own");
        string tests = ModSource(@"ParkSpaces\SpaceDirector.Tests.cs");
        int day = tests.IndexOf("public void TestDay()", StringComparison.Ordinal);
        string body = day > 0 ? tests.Substring(day, tests.IndexOf("public void TestRival()", day, StringComparison.Ordinal) - day) : string.Empty;
        Check(day > 0 && !body.Contains("LastHour") && !body.Contains("OnHour(") && !body.Contains("AddHour") && body.Contains("SpaceRules.RefundDue(_state, day, SpaceGame.HasVehicle)") && body.Contains("Pass(day);"),
            "parking spaces the test button: the test counts no hour of its own and says what the game charged it for");
    }

    // ---- the hour at the end of a rental

    private static void SpaceRentEndChecks()
    {
        SpacePark park = BizPark(2, 12, 0);
        var parks = new List<SpacePark> { park };
        var s = new SpaceState { Seed = 5 };
        SpaceHolding rented = SpaceRules.Take(s, park, 1, SpaceHolding.PlayerIs, 10, 0, park.RentMonth);
        var scratch = new List<string>();
        Action<int, List<string>> one = (n, into) => into.Add("car-a");
        int last = SpaceDay.Hour(s, parks, rented.PaidTo * 24 + 23, rented.PaidTo, one, scratch);
        int free = SpaceDay.Hour(s, parks, (rented.PaidTo + 1) * 24, rented.PaidTo + 1, one, scratch);   // hour 0 of the day after the paid term, before the pass has ended the space
        Check(last == 1 && free == 0, "parking spaces rent: the last hour of the paid term is covered, the first hour of the day after it is not, even before the pass has ended the space");
        var book = new BizLedger { Cash = 0 };
        SpaceDayResult r = SpaceDay.Pass(s, parks, rented.PaidTo + 1, false, id => true, book);
        Check(r.Ended == 1 && s.Held.Count == 0 && book.Notes.Contains(SpaceNote.Unpaid) && book.TookOf(SpaceLine.Rent) == 0 && r.Refund == SpaceCatalogue.HourFee, "parking spaces rent: with no cash the space ends at the pass, having covered the one hour of its last day and none after");
        // Renewed: the term runs on from the day after, and the cash is taken once.
        var s2 = new SpaceState { Seed = 5 };
        SpaceHolding again = SpaceRules.Take(s2, park, 1, SpaceHolding.PlayerIs, 10, 0, park.RentMonth);
        var rich = new BizLedger { Cash = park.RentMonth * 3L };
        SpaceDay.Pass(s2, parks, again.PaidTo + 1, false, id => true, rich);
        Check(s2.Held.Count == 1 && rich.TookOf(SpaceLine.Rent) == park.RentMonth && again.PaidTo == 10 + 2 * SpaceCatalogue.RentDays - 1 && s2.PaidTotal == park.RentMonth,
            "parking spaces rent: with the cash the next term is taken once, from the day after the last");
        // A rented space given up, and a rented space in a building that has become the player's: ended at the term, nothing paid back.
        var s3 = new SpaceState { Seed = 5 };
        SpaceHolding ending = SpaceRules.Take(s3, park, 1, SpaceHolding.PlayerIs, 10, 0, park.RentMonth);
        ending.Ending = true;
        var b3 = new BizLedger { Cash = 100000 };
        SpaceDayResult e = SpaceDay.Pass(s3, parks, ending.PaidTo + 1, false, id => true, b3);
        SpacePark mine = BizPark(2, 12, 0, SpaceOwner.Player);
        var s4 = new SpaceState { Seed = 5 };
        SpaceHolding own = SpaceRules.Take(s4, park, 1, SpaceHolding.PlayerIs, 10, 0, park.RentMonth);
        var b4 = new BizLedger { Cash = 100000 };
        SpaceDay.Pass(s4, new List<SpacePark> { mine }, own.PaidTo - 5, false, id => true, b4);
        bool kept = s4.Held.Count == 1;
        SpaceDay.Pass(s4, new List<SpacePark> { mine }, own.PaidTo + 1, false, id => true, b4);
        Check(e.Ended == 1 && b3.TookOf(SpaceLine.Rent) == 0 && kept && s4.Held.Count == 0 && b4.TookOf(SpaceLine.Rent) == 0 && b4.GotOf(SpaceLine.RentBack) == 0 && b4.Notes.Contains(SpaceNote.Ended),
            "parking spaces rent: a rental given up, and one in a building that became the player's, run to the end of the paid term and then end, with no rent taken and none paid back");
    }

    // ---- every way a bought or rented space ends

    private static void SpaceEndingChecks()
    {
        // Bought in a high market, then the player buys the building and the market has fallen: settled at once at the lower price less the fee, never the old price.
        SpacePark dear = BizPark(5, 20, 0, SpaceOwner.City, 1800), cheap = BizPark(5, 20, 0, SpaceOwner.City, 900);
        var s = new SpaceState { Seed = 9 };
        int slot = SpaceRules.FreeSlot(s, dear, 10);
        SpaceHolding h = SpaceRules.Take(s, dear, slot, SpaceHolding.PlayerIs, 10, dear.Price, 0);
        SpaceRules.Looked(h, dear, 11);
        cheap.Owner = SpaceOwner.Player;
        var book = new BizLedger { Cash = 0 };
        int owned = SpaceDay.Owned(s, new List<SpacePark> { cheap }, 12, false, book);
        long expect = SpaceRules.Back(dear.Price, cheap.Price);
        Check(owned == 1 && s.Held.Count == 0 && book.Cash == expect && expect < cheap.Price && expect < dear.Price && book.GotOf(SpaceLine.Sold) == expect && book.Notes.Contains(SpaceNote.OwnBuilding),
            "parking spaces ending: a space bought dear in a building the player then buys is settled at once at the lower of what was paid and today's price, less the fee, and the player is told (" + book.Cash + " of " + dear.Price + ")");
        // ...and then the building is demolished: nothing is left to settle, so the old price never comes back.
        SpaceDayResult later = SpaceDay.Pass(s, new List<SpacePark>(), 20, false, id => true, book);
        Check(later.Gone == 0 && book.Cash == expect && book.GotOf(SpaceLine.Gone) == 0, "parking spaces ending: when that building is demolished later no old price comes back");
        // Buying and settling is never a profit, whatever the market did in between.
        bool noProfit = true;
        for (int low = 400; low <= 2400; low += 200)
        {
            for (int high = 400; high <= 2400; high += 200)
            {
                SpacePark a = BizPark(5, 20, 0, SpaceOwner.City, high), b = BizPark(5, 20, 0, SpaceOwner.Player, low);
                var st = new SpaceState { Seed = 9 };
                SpaceHolding hh = SpaceRules.Take(st, a, SpaceRules.FreeSlot(st, a, 10), SpaceHolding.PlayerIs, 10, a.Price, 0);
                var l = new BizLedger { Cash = 0 };
                SpaceDay.Owned(st, new List<SpacePark> { b }, 11, false, l);
                noProfit &= l.Cash < a.Price && l.Cash <= b.Price && st.Held.Count == 0;
            }
        }
        Check(noProfit, "parking spaces ending: buying a space in any market and then the building never returns as much as the space cost");
        // A car park that is gone because somebody else took it away (the city rebuilt it): what was paid, in full and no more; the unused days of a rent.
        var gs = new SpaceState { Seed = 9 };
        SpaceHolding bought = SpaceRules.Take(gs, dear, SpaceRules.FreeSlot(gs, dear, 10), SpaceHolding.PlayerIs, 10, dear.Price, 0);
        SpaceHolding rent = SpaceRules.Take(gs, dear, SpaceRules.FreeSlot(gs, dear, 10), SpaceHolding.PlayerIs, 10, 0, dear.RentMonth);
        var parks = new List<SpacePark> { dear };
        SpaceDay.Settle(gs, parks, 11, new BizLedger());
        var none = new List<SpacePark>();
        var gone = new BizLedger { Cash = 0 };
        int early = SpaceDay.Settle(gs, none, 20, gone);
        int on = SpaceDay.Settle(gs, none, 20 + SpaceCatalogue.GoneDays - 1, gone);
        int settled = SpaceDay.Settle(gs, none, 20 + SpaceCatalogue.GoneDays, gone);
        long unused = dear.RentMonth * (rent.PaidTo - 20 + 1) / SpaceCatalogue.RentDays;
        Check(early == 0 && on == 0 && settled == 2 && gs.Held.Count == 0 && gone.GotOf(SpaceLine.Gone) == dear.Price && gone.GotOf(SpaceLine.RentBack) == unused && unused > 0 && unused < dear.RentMonth,
            "parking spaces ending: a car park somebody else took away is settled after " + SpaceCatalogue.GoneDays + " days, a bought space at what was paid and a rented one for the days unused since it went");
        // The rent comes back where it was booked, not as capital; a space bought, sold back or settled is capital.
        Check(!DayRules.IsTransfer(SpaceDay.KeyOf(SpaceLine.RentBack)) && SpaceDay.KeyOf(SpaceLine.RentBack) == SpaceDay.KeyOf(SpaceLine.Rent) && !DayRules.IsTransfer(SpaceDay.KeyOf(SpaceLine.Refund))
              && DayRules.IsTransfer(SpaceDay.KeyOf(SpaceLine.Gone)) && DayRules.IsTransfer(SpaceDay.KeyOf(SpaceLine.Sold)) && DayRules.IsTransfer(SpaceDay.KeyOf(SpaceLine.Buy)),
            "parking spaces ending: a rent paid back is booked where the rent was (a cost in the day's result), a space bought, sold back or settled is capital");
        // The building was the player's when last seen: the car park going is their doing, so the lower price and the fee apply, not the full price.
        SpacePark seenOwn = BizPark(5, 20, 0, SpaceOwner.Player, 900);
        var os = new SpaceState { Seed = 9 };
        SpaceHolding hs = SpaceRules.Take(os, dear, SpaceRules.FreeSlot(os, dear, 10), SpaceHolding.PlayerIs, 10, dear.Price, 0);
        SpaceRules.Looked(hs, seenOwn, 15);
        var ob = new BizLedger();
        SpaceDay.Settle(os, none, 16, ob);
        SpaceDay.Settle(os, none, 16 + SpaceCatalogue.GoneDays, ob);
        Check(os.Held.Count == 0 && ob.GotOf(SpaceLine.Gone) == SpaceRules.Back(dear.Price, seenOwn.Price) && ob.GotOf(SpaceLine.Gone) < dear.Price,
            "parking spaces ending: a car park that goes from under a building the player owned is settled at the lower of the price paid and the last price seen, less the fee");
        // A tycoon's space: in a car park somebody else took away, they get what they paid and their worth does not move; where the player's building took it, the fee is lost.
        var ts = new SpaceState { Seed = 9 };
        SpaceHolding theirs = SpaceRules.Take(ts, dear, SpaceRules.FreeSlot(ts, dear, 10), 2, 10, dear.Price, 0);
        var tb = new BizLedger();
        SpaceDay.Settle(ts, none, 11, tb);
        SpaceDay.Settle(ts, none, 11 + SpaceCatalogue.GoneDays, tb);
        var ts2 = new SpaceState { Seed = 9 };
        SpaceRules.Take(ts2, dear, SpaceRules.FreeSlot(ts2, dear, 10), 2, 10, dear.Price, 0);
        var tb2 = new BizLedger();
        SpaceDay.Owned(ts2, new List<SpacePark> { cheap }, 12, false, tb2);
        Check(ts.Held.Count == 0 && tb.Tycoons.Count == 1 && tb.Tycoons[0].Key == 2 && tb.Tycoons[0].Value == 0 && ts2.Held.Count == 0 && tb2.Tycoons.Count == 1 && tb2.Tycoons[0].Value < 0 && tb2.Cash == 0,
            "parking spaces ending: a tycoon's space is settled by the same rule, their worth unmoved when the car park was taken away and down by the loss when the player's building took it");
    }

    // ---- a shared city

    private static void SpaceSessionChecks()
    {
        SpacePark dear = BizPark(5, 20, 0, SpaceOwner.City, 1800), own = BizPark(6, 20, 0, SpaceOwner.Player, 900);
        var s = new SpaceState { Seed = 9, LastDay = 30 };
        SpaceHolding mine = SpaceRules.Take(s, dear, 1, SpaceHolding.PlayerIs, 10, dear.Price, 0);
        SpaceHolding rent = SpaceRules.Take(s, dear, 2, SpaceHolding.PlayerIs, 10, 0, dear.RentMonth);
        SpaceHolding goneOne = SpaceRules.Take(s, BizPark(7, 20, 0), 1, SpaceHolding.PlayerIs, 10, 90000, 0);
        SpaceHolding theirs = SpaceRules.Take(s, own, 3, 2, 10, 90000, 0);
        SpaceHolding ofGone = SpaceRules.Take(s, BizPark(7, 20, 0), 2, 3, 10, 90000, 0);
        SpaceRules.AddHour(s, 30, 5, "car-a");
        var parks = new List<SpacePark> { dear, own };   // car park 7 is not in the list: the city's buildings are the host's, and this machine cannot say it is gone
        var book = new BizLedger { Cash = 100000 };
        for (int day = 31; day <= 31 + SpaceCatalogue.GoneDays + 2; day++)
        {
            SpaceDayResult r = SpaceDay.Pass(s, parks, day, true, id => true, book);
            if (day == 31)
            {
                Check(r.Refund == SpaceCatalogue.HourFee, "parking spaces a shared city: the player's own charges are paid back in a session (" + r.Refund + ")");
            }
        }
        bool rentRan = book.TookOf(SpaceLine.Rent) == 0;   // the term runs to day 39 and the days passed stop at 36: no rent was due yet
        var later = new BizLedger { Cash = 100000 };
        SpaceDay.Pass(s, parks, rent.PaidTo + 1, true, id => true, later);
        Check(rentRan && later.TookOf(SpaceLine.Rent) == dear.RentMonth && s.Held.Contains(rent), "parking spaces a shared city: a rented space goes on running on the player's own money, its next term taken");
        Check(s.Held.Contains(mine) && s.Held.Contains(goneOne) && s.Held.Contains(theirs) && s.Held.Contains(ofGone) && book.Tycoons.Count == 0 && later.Tycoons.Count == 0
              && book.GotOf(SpaceLine.Gone) == 0 && book.GotOf(SpaceLine.Sold) == 0 && goneOne.Missing == 0 && book.Notes.Count == 0,
            "parking spaces a shared city: no car park is settled as gone, no building as the player's, no tycoon's worth moves and nobody is told, however many days pass");
        // The same days outside a session do settle them: the rules differ only by the session.
        var out1 = new BizLedger();
        int outDay = rent.PaidTo + 5;
        SpaceDay.Pass(s, parks, outDay, false, id => true, out1);
        SpaceDay.Pass(s, parks, outDay + SpaceCatalogue.GoneDays, false, id => true, out1);
        Check(!s.Held.Contains(goneOne) && !s.Held.Contains(ofGone) && !s.Held.Contains(theirs) && out1.Tycoons.Count == 2 && out1.GotOf(SpaceLine.Gone) == 90000,
            "parking spaces a shared city: out of the session the same pass settles the car park gone, and the tycoon's space in the player's building (down by the fee)");
        string director = ModSource(@"ParkSpaces\SpaceDirector.cs");
        Check(director.Contains("SpaceDay.Pass(_state, _parks, day, session, SpaceGame.HasVehicle, _books)") && director.Contains("int moves = session ? 0 : Rivals(day);") && director.Contains("SpaceDay.Owned(_state, _parks, day, GoingPublic.InSession, _books)"),
            "parking spaces a shared city: the director gives the pass the session and the tycoons rest");
    }

    // ---- a car park that shrinks

    private static void SpaceShrunkChecks()
    {
        SpacePark big = BizPark(4, 30, 0), small = BizPark(4, 8, 0);
        var s = new SpaceState { Seed = 3 };
        SpaceHolding high = SpaceRules.Take(s, big, 25, SpaceHolding.PlayerIs, 10, 0, big.RentMonth);
        SpaceHolding low = SpaceRules.Take(s, big, 2, SpaceHolding.PlayerIs, 10, 0, big.RentMonth);
        SpaceHolding boughtHigh = SpaceRules.Take(s, big, 26, SpaceHolding.PlayerIs, 10, big.Price, 0);
        high.PaidTo = 21;   // its term is over on the 22nd
        low.PaidTo = 21;
        var parks = new List<SpacePark> { small };   // the building was rebuilt smaller: 8 spaces
        var book = new BizLedger { Cash = 100000 };
        SpaceDay.Pass(s, parks, 20, false, id => true, book);
        bool marked = high.Missing == 20 && boughtHigh.Missing == 20 && low.Missing == 0;
        SpaceDayResult due = SpaceDay.Pass(s, parks, 22, false, id => true, book);   // both rented terms are over; a space that is no longer there is not asked for
        bool oneRent = book.TookOf(SpaceLine.Rent) == big.RentMonth && s.Held.Contains(high) && low.PaidTo == 21 + SpaceCatalogue.RentDays;
        SpaceDayResult gone = SpaceDay.Pass(s, parks, 20 + SpaceCatalogue.GoneDays, false, id => true, book);
        Check(marked && due.Gone == 0 && oneRent, "parking spaces a smaller car park: a space above the new number of spaces is marked and takes no rent, the one that is still there pays its rent");
        long unused = big.RentMonth * (21 - 20 + 1) / SpaceCatalogue.RentDays;
        Check(gone.Gone == 2 && !s.Held.Contains(high) && !s.Held.Contains(boughtHigh) && s.Held.Contains(low) && book.GotOf(SpaceLine.Gone) == big.Price && book.GotOf(SpaceLine.RentBack) == unused && unused > 0,
            "parking spaces a smaller car park: after the days of grace it is settled like a space whose car park has gone, the bought one at what was paid and the rented one for its unused days; the others stay");
    }

    // ---- the managers' morning

    private static void SupplyMorningChecks()
    {
        var s = new DealerState { Seed = 99 };
        DealerYard a = SupplyYard("A~1", 8), b = SupplyYard("B~2", 6);
        s.Yards.Add(a);
        s.Yards.Add(b);
        int id = 1;
        foreach (int price in new[] { 16000, 16500, 17000, 17500, 18000 })
        {
            s.Market.Add(new DealerListing { Id = id++, Kind = ListingKind.Wholesale, Type = SupplyModelsOf[0].Type, Km = 20, Condition = 1000, Shown = 1000, Inspected = true, Asking = price });
        }
        var sup = new CarSupplyState();
        sup.KeepOf("A~1", true).Keep[0] = 2;
        sup.KeepOf("B~2", true).Keep[0] = 1;
        var orders = new List<CarSupplyOrder>();
        bool sick = true;
        bool aDecides = true;
        Func<DealerYard, bool> decides = y => y.Place == "A~1" ? aDecides && !sick : !sick;
        // A day nobody decides (the manager off sick, nobody standing in) is not marked, so nothing is ordered and the next hour asks again.
        CarSupplyRules.Morning(s, sup, SupplyModelsOf, 40, 40 * 24 + 8, 1000000, true, decides, orders);
        Check(orders.Count == 0 && sup.KeepOf("A~1", false).Day == -1 && sup.KeepOf("B~2", false).Day == -1, "car suppliers standing order: a day nobody decides orders nothing and is not marked as ordered");
        sick = false;
        long left = CarSupplyRules.Morning(s, sup, SupplyModelsOf, 40, 40 * 24 + 9, 1000000, true, decides, orders);
        Check(orders.Count == 3 && orders.Count(o => o.Place == "A~1") == 2 && orders.Count(o => o.Place == "B~2") == 1 && sup.KeepOf("A~1", false).Day == 40 && sup.KeepOf("B~2", false).Day == 40
              && left == 1000000 - orders.Sum(o => (long)o.Price) && orders.Select(o => o.Listing).Distinct().Count() == 3,
            "car suppliers standing order: once the manager can decide the next hour orders for the showroom, each car for its showroom and none twice");
        var again = new List<CarSupplyOrder>();
        CarSupplyRules.Morning(s, sup, SupplyModelsOf, 40, 40 * 24 + 10, left, true, decides, again);
        CarSupplyRules.Morning(s, sup, SupplyModelsOf, 40, 40 * 24 + 23, left, true, decides, again);
        Check(again.Count == 0, "car suppliers standing order: once a game day, however often the hour is worked or the save loaded");
        CarSupplyRules.Morning(s, sup, SupplyModelsOf, 41, 41 * 24 + 8, 1000000, true, decides, again);
        Check(again.Count == 3 && again.All(o => o.Place != null) && sup.KeepOf("A~1", false).Day == 41 && sup.KeepOf("B~2", false).Day == 41,
            "car suppliers standing order: the next day asks again (nothing was bought in this rule's run, so the same three are short)");
        // One showroom's manager is off sick and the other's is not: only the showroom that was decided for is marked.
        var s2 = new DealerState { Seed = 99 };
        s2.Yards.Add(SupplyYard("A~1", 8));
        s2.Yards.Add(SupplyYard("B~2", 6));
        s2.Market.AddRange(s.Market.Select(l => new DealerListing { Id = l.Id, Kind = l.Kind, Type = l.Type, Km = 20, Condition = 1000, Shown = 1000, Inspected = true, Asking = l.Asking }));
        var sup2 = new CarSupplyState();
        sup2.KeepOf("A~1", true).Keep[0] = 2;
        sup2.KeepOf("B~2", true).Keep[0] = 1;
        aDecides = false;
        sick = false;
        var o2 = new List<CarSupplyOrder>();
        CarSupplyRules.Morning(s2, sup2, SupplyModelsOf, 50, 50 * 24 + 8, 1000000, true, decides, o2);
        bool onlyB = o2.All(o => o.Place == "B~2") && sup2.KeepOf("A~1", false).Day == -1 && sup2.KeepOf("B~2", false).Day == 50;
        aDecides = true;
        CarSupplyRules.Morning(s2, sup2, SupplyModelsOf, 50, 50 * 24 + 9, 1000000, true, decides, o2);
        Check(onlyB && o2.Count(o => o.Place == "A~1") == 2 && o2.Count(o => o.Place == "B~2") == 1, "car suppliers standing order: a showroom whose manager could not decide is ordered for when they can, the others once");
        // The option off: nothing is ordered and nothing is marked.
        var off = new List<CarSupplyOrder>();
        var sup3 = new CarSupplyState();
        sup3.KeepOf("A~1", true).Keep[0] = 2;
        CarSupplyRules.Morning(s2, sup3, SupplyModelsOf, 60, 60 * 24 + 8, 1000000, false, decides, off);
        Check(off.Count == 0 && sup3.KeepOf("A~1", false).Day == -1, "car suppliers standing order: with the option off a manager orders nothing and nothing is marked");
        // The cap of deliveries before money: with every delivery taken, the shipped order is refused by the rule the director asks before it takes any money.
        var full = new DealerState();
        Check(!CarSupplyRules.WaysFull(full), "car suppliers deliveries: with room for another delivery an order is taken");
        while (full.Deliveries.Count < DealerCatalogue.DeliveriesMost)
        {
            full.Deliveries.Add(new DealerDelivery { Id = 500 + full.Deliveries.Count, Place = "Z~9", Type = "van", Cost = 1, Due = 600 });
        }
        Check(CarSupplyRules.WaysFull(full) && DealerCatalogue.DeliveriesMost == 16, "car suppliers deliveries: with " + DealerCatalogue.DeliveriesMost + " on their way a supplier's order is refused");
        // Arrival as a new car: a supplier's delivery comes onto the lot as a new car from the wholesale market, whatever the option says by then; an auction lot as an auction car.
        var arrive = new CarSupplyState();
        arrive.Ordered(57, CarSupplyCatalogue.Importer);
        Check(CarSupplyRules.SourceOf(arrive, 57) == CarSource.Wholesale && CarSupplyRules.SourceOf(arrive, 58) == CarSource.Auction, "car suppliers deliveries: a supplier's car arrives as a new car, an auction lot as an auction car");
        arrive.Arrived(57);
        Check(CarSupplyRules.SourceOf(arrive, 57) == CarSource.Auction && CarSupplyRules.SourceOf(CarSupplyStore.Read(CarSupplyStore.Write(arrive), out int _), 57) == CarSource.Auction,
            "car suppliers deliveries: a car that has arrived is no longer on order, across a save");
        string dir = ModSource(@"Economy\Dealer\Supply\DealerDirector.Supply.cs");
        Check(dir.Contains("CarSupplyRules.WaysFull(_state)") && dir.Contains("CarSupplyRules.SourceOf(_supply, q.Id)") && dir.Contains("CarSupplyRules.Morning(_state, _supply, _models, day, now, DealerGame.Cash, SupplyOn, decides, _orders)")
              && dir.Contains("if (Buy(o.Listing, o.Place, o.Price))"), "car suppliers standing order: the director asks these rules and buys every car through the one purchase path");
        string wire = ModSource(@"Wiring\Wiring.BizAsksLinks.cs"), mgr = ModSource(@"Economy\Company\CompanyDirector.Managers.cs");
        Check(wire.Contains("company.Decides(place, ManagerTask.Stock)") && !wire.Contains("company.Manages(") && mgr.Contains("CompanyDay.Deciding(this, mp.Place.Place, mp.Kind, mp.Own, task, out _, out _, out _)"),
            "car suppliers standing order: the manager is asked through the door every other decision of a manager passes (a sick manager with no stand-in decides nothing)");
    }

    // ---- the other dealers: their cars on their way, and their prices with the suppliers on as shipped

    private static void SupplyDealersChecks()
    {
        bool waiting = true, never = false;
        int seen = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var st = new DealerState { Seed = seed };
            for (int day = 2; day <= 80; day++)
            {
                var onWay = st.Rivals.Where(c => c.Day >= day).ToList();   // not on the lot yet this morning
                DealerDay.Morning(st, day, SupplyModelsOf, null, 1000, true, true);
                foreach (RivalCar c in onWay)
                {
                    seen++;
                    waiting &= st.Rivals.Any(x => x.Rival == c.Rival && x.Type == c.Type && x.Ask == c.Ask && x.Day == c.Day);   // still ordered: it was not sold before it came
                }
                never |= st.Rivals.Any(c => c.Day > day + DealerCatalogue.DeliveryDaysMost);
            }
        }
        Check(waiting && !never && seen > 200, "car suppliers other dealers: a car on its way is not sold before it arrives, nor on the morning it arrives, over sixty saves and eighty days (" + seen + " cars on their way)");
        // Prices with the suppliers on as shipped: each ask is inside the bounds the pricing checks hold, and within the suppliers' span of the same car without them.
        bool inside = true, span = true;
        int looked = 0;
        for (int seed = 1; seed <= 120; seed++)
        {
            var on = new DealerState { Seed = seed };
            var off = new DealerState { Seed = seed };
            DealerDay.Morning(on, 10, SupplyModelsOf, null, 1000, true, true);
            DealerDay.Morning(off, 10, SupplyModelsOf, null, 1000, true, false);
            for (int i = 0; i < on.Rivals.Count && i < off.Rivals.Count; i++)
            {
                RivalCar a = on.Rivals[i], b = off.Rivals[i];
                looked++;
                inside &= a.Ask > 0 && a.Ask % DealerCatalogue.Round == 0 && a.Ask >= 1 && a.Day >= 10 && a.Day <= 10 + DealerCatalogue.DeliveryDaysMost;
                span &= a.Type == b.Type && a.Ask * 1000L >= (long)b.Ask * (CarSupplyCatalogue.PriceLeast - 20) && a.Ask * 1000L <= (long)b.Ask * (CarSupplyCatalogue.PriceMost + 20);
            }
        }
        Check(inside && span && looked > 1000, "car suppliers other dealers: with the suppliers on as shipped a rival's ask is a whole price in fifties, positive, and within the suppliers' span of what it asks without them (" + looked + " cars)");
        // The dealers' price checks of the dealership, once more with the suppliers on: the counts, the price level and the cheapest.
        W4Suppliers = true;
        try
        {
            W4Rivals();
        }
        finally
        {
            W4Suppliers = false;
        }
    }
}
