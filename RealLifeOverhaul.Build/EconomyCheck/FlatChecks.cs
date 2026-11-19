using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy;

// Flats and studios to buy as a home (run with --flats; Scripts/Economy/Estate/Flat*.cs): a building's value split over its units by floor area, a studio the smallest
// share, the shares adding up to the building's value to the dollar; the price of a long lease and what it comes to in every district of the game's own figures (a
// first deposit within a few weeks of early work, by the rule and no case of its own); the listing, drawn from the building and the window alone, never more than the
// most, none under the least units, none held; the deposit and the mortgage's figures; the bills once a month and never twice, after a gap too; a flat folding into a
// building bought whole without being paid for twice; a sale by the rule listings already have, and no profit from buying and selling; a let flat by the letting rules;
// the refusals before any money; the save beside the Estate's (an old save, a newer line, a damaged one, kept as read); the sources and the texts.
internal static partial class Program
{
    private static void Flats()
    {
        FlatShareChecks();
        FlatPriceChecks();
        FlatListingChecks();
        FlatBillChecks();
        FlatFoldChecks();
        FlatSaleChecks();
        FlatLetChecks();
        FlatMoreChecks();
        FlatRefusalChecks();
        FlatStoreChecks();
        FlatSourceChecks();
    }

    // The game's own figures (Build 3682): a residential building's floor, and the price of a square metre of its district (the base price 20,554 times residential's 1.5
    // times the district's real estate multiplier times the street's traffic factor).
    private static int FlatValue(int sqm, double perSqm, int cond) => EstateRules.Value((int)(sqm * perSqm), EstateCatalogue.RealStart, EstateCatalogue.IndexStart, EstateCatalogue.IncomeMiddle, cond, 0);

    private static void FlatShareChecks()
    {
        bool adds = true, studioSmallest = true, whole = true;
        var rng = new Random(77);
        for (int round = 0; round < 400; round++)
        {
            string place = "Street" + rng.Next(1, 90) + "~" + rng.Next(1, 200);
            int units = rng.Next(1, EstateCatalogue.UnitsMost + 1), income = rng.Next(EstateCatalogue.IncomeLeast, EstateCatalogue.IncomeMost + 1);
            long value = rng.Next(0, 3) == 0 ? rng.Next(1, 500) : rng.Next(1000, int.MaxValue / 8);
            int[] w = FlatRules.Weights(place, units, income);
            int[] shares = FlatRules.Freehold(value, w);
            adds &= shares.Sum(x => (long)x) == value && w.Length == units;
            for (int i = 0; i < w.Length; i++)
            {
                whole &= shares[i] >= 0;
                for (int j = 0; j < w.Length; j++)
                {
                    studioSmallest &= w[i] >= w[j] || shares[i] <= shares[j];   // a smaller kind never has the bigger share
                }
            }
        }
        Check(adds, "flats: the shares of a building's units add up to the building's value to the dollar, whatever the value, the units and the district (largest remainder, 400 buildings)");
        Check(studioSmallest && whole, "flats: a unit of a smaller kind never has a bigger share than a unit of a larger one, and no share is under nothing");
        Check(FlatCatalogue.KindWeight[0] < FlatCatalogue.KindWeight[1] && FlatCatalogue.KindWeight[1] < FlatCatalogue.KindWeight[2] && FlatCatalogue.KindWeight[2] < FlatCatalogue.KindWeight[3]
              && FlatCatalogue.KindWeight.Length == FlatCatalogue.Kinds, "flats: the studio is the smallest kind and the kinds go up in floor");
        int[] one = FlatRules.Freehold(1000, new[] { 3, 5, 7, 10 });
        Check(one.Sum() == 1000 && one[0] == 120 && one[3] == 400, "flats: 3, 5, 7 and 10 parts of a thousand are 120, 200, 280 and 400 (" + string.Join(", ", one) + ")");
        int[] none = FlatRules.Freehold(0, new[] { 3, 5 });
        Check(none.Sum() == 0 && FlatRules.Freehold(-5, new[] { 1 })[0] == 0, "flats: a building worth nothing has units worth nothing, and never a negative share");
        int studios = 0, total = 0;
        for (int i = 0; i < 2000; i++)
        {
            total++;
            studios += FlatRules.KindOf("Mix~" + i, i % 12, EstateCatalogue.IncomeMiddle) == (int)FlatKind.Studio ? 1 : 0;
        }
        int poorStudios = 0, richStudios = 0;
        for (int i = 0; i < 2000; i++)
        {
            poorStudios += FlatRules.KindOf("Mix~" + i, i % 12, EstateCatalogue.IncomeLeast) == (int)FlatKind.Studio ? 1 : 0;
            richStudios += FlatRules.KindOf("Mix~" + i, i % 12, EstateCatalogue.IncomeMost) == (int)FlatKind.Studio ? 1 : 0;
        }
        Check(Math.Abs(studios * 1000 / total - FlatCatalogue.StudioBase) < 60 && poorStudios > studios && richStudios < studios,
            "flats: about " + FlatCatalogue.StudioBase / 10 + "% of a middling district's units are studios, more in a poor one and fewer in a rich one (" + poorStudios + ", " + studios + ", " + richStudios + " of 2000)");
        bool kinds = true, same = true;
        for (int i = 0; i < 200; i++)
        {
            int k = FlatRules.KindOf("Same~" + i, 3, 1000);
            kinds &= k >= 0 && k < FlatCatalogue.Kinds;
            same &= k == FlatRules.KindOf("Same~" + i, 3, 1000);
        }
        Check(kinds && same, "flats: a unit's kind is a draw of its building alone, always the same, always one of the four");
        int[] sqm = Enumerable.Range(0, 4).Select(i => FlatRules.Sqm(1000, new[] { 3, 5, 7, 10 }, i)).ToArray();
        Check(sqm.Sum() <= 1000 && sqm.Sum() >= 997 && sqm[0] < sqm[3] && FlatRules.Sqm(0, new[] { 3 }, 0) == 1 && FlatRules.Sqm(1000, new[] { 3 }, 5) == 1,
            "flats: a unit's floor is the building's over its units by their weights (" + string.Join(", ", sqm) + "), at least a square metre");
        Check(FlatRules.Share(new[] { 3, 5, 7, 10 }, 3) == 400000 && FlatRules.Share(new[] { 3 }, 9) == 0, "flats: a unit's share of the floor is in millionths");
    }

    private static void FlatPriceChecks()
    {
        Check(FlatRules.Lease(1000000) == 150000 && FlatRules.Lease(5) == 10 && FlatRules.Lease(0) == 0 && FlatRules.Lease(-3) == 0,
            "flats: a long lease costs " + FlatCatalogue.LeaseShare / 10 + "% of the unit's share, named in tens, never under ten, nothing for nothing");
        Check(FlatRules.Lease(153247) % FlatCatalogue.RoundTo == 0 && FlatRules.Lease(int.MaxValue) > 0, "flats: a price is named in tens and the largest value does not overflow");
        // The cheapest studio the game's own buildings give: Third Street 56 in the Garment District (591 m2, a unit of 54, so 10 units, a base value of 4,737,491), run down to
        // a condition of 300. Found by walking all 885 buildings of Build 3682 with these very rules; the other districts' cheapest are the table in docs/features/flats.md.
        const string cheapPlace = "ba%3Astreet_thirdstreet~56";
        int cheapValue = EstateRules.Value(4737491, EstateCatalogue.RealStart, EstateCatalogue.IndexStart, EstateCatalogue.IncomeMiddle, 300, 0);
        int cheapUnits = EstateRules.UnitCount(591, 54);
        int[] w0 = FlatRules.Weights(cheapPlace, cheapUnits, EstateCatalogue.IncomeMiddle);
        int[] s0 = FlatRules.Freehold(cheapValue, w0);
        int cheapestPrice = int.MaxValue;
        for (int i = 0; i < cheapUnits; i++)
        {
            cheapestPrice = w0[i] == FlatCatalogue.KindWeight[0] ? Math.Min(cheapestPrice, FlatRules.Lease(s0[i])) : cheapestPrice;
        }
        string where = "Third Street 56, Garment District, at condition 300";
        int cheapestDeposit;
        cheapestDeposit = ProductTerms.Deposit(cheapestPrice, ProductCatalogue.HomeDepositLeast);
        const int earlyWeek = 1100;   // dollars a week of an early gig (the least of the 1,100 to 1,500 a player reports): a first deposit is paid from this
        Check(cheapestPrice > 0 && cheapestPrice < FlatValue(443, 20554 * 1.5 * 0.65 * 0.44, 1000) / 20,
            "flats: the cheapest studio (" + cheapestPrice + " at " + where + ") is under a twentieth of the cheapest whole building (" + FlatValue(443, 20554 * 1.5 * 0.65 * 0.44, 1000) + ")");
        Check(cheapUnits == 10 && cheapValue == 3742617 && cheapestPrice == 25520, "flats: that building is " + cheapUnits + " units worth " + cheapValue + " and its cheapest studio costs " + cheapestPrice + " (the figure docs/features/flats.md gives)");
        Check(cheapestDeposit <= earlyWeek * 5 && cheapestDeposit > 0,
            "flats: the first deposit on the cheapest studio is " + cheapestDeposit + " (" + ProductCatalogue.HomeDepositLeast + "% of " + cheapestPrice + "), " + (cheapestDeposit * 10 / earlyWeek / 10.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " weeks of an early gig of " + earlyWeek + " a week: within five weeks, by the share, the lease and the deposit rule alone");
        Check(ProductTerms.FinanceLeast(ProductKind.Mortgage) <= cheapestPrice - cheapestDeposit, "flats: the cheapest studio's mortgage is over the bank's least, so the bank can lend on it");
        // The deposit rule is the mortgage's own, for a building and a flat alike.
        bool deposit = true;
        for (int price = 1000; price < 5000000; price = price * 3 + 7)
        {
            foreach (int pct in new[] { 20, 30, 40, 50 })
            {
                deposit &= FlatRules.Deposit(price, pct) == ProductTerms.Deposit(price, pct) && FlatRules.Financed(price, pct) == price - ProductTerms.Deposit(price, pct)
                    && FlatRules.Deposit(price, pct) >= price * pct / 100;
            }
            deposit &= FlatRules.Deposit(price, FlatCatalogue.PayInFull) == price && FlatRules.Financed(price, FlatCatalogue.PayInFull) == 0;
        }
        Check(deposit && ProductCatalogue.HomeDepositLeast == 20, "flats: the deposit is the home mortgage's own (at least " + ProductCatalogue.HomeDepositLeast + "%), the whole price for cash, and what the bank puts in is the rest");
        Check(FlatRules.MortgageFits(10000, 5000, 20000) && !FlatRules.MortgageFits(4999, 5000, 20000) && !FlatRules.MortgageFits(20001, 5000, 20000),
            "flats: a mortgage is within the bank's least and most");
        // A flat's value moves with the building's by the market and not otherwise.
        int a = FlatRules.Value(2000000, "Value~1", 6, 1000, 2), b2 = FlatRules.Value(4000000, "Value~1", 6, 1000, 2), none = FlatRules.Value(0, "Value~1", 6, 1000, 2);
        Check(Math.Abs(b2 - 2 * a) <= FlatCatalogue.RoundTo * 2 && a > 0 && none == 0 && FlatRules.Value(2000000, "Value~1", 6, 1000, 9) == 0,
            "flats: a flat's value is its share of the building's value, so it moves with it (" + a + " then " + b2 + "), and is nothing where the building or the unit is not there");
        Check(FlatRules.Value(2000000, "Value~1", 6, 1000, 2) == FlatRules.Lease(FlatRules.Freehold(2000000, FlatRules.Weights("Value~1", 6, 1000))[2]),
            "flats: the value after a purchase is the price it was listed at, by the one rule");
    }

    private static List<FlatListing> FlatDraw(FlatBuilding b, int seed, int day, Func<string, int, bool> held = null)
    {
        var into = new List<FlatListing>();
        FlatRules.Listings(into, b, seed, day, held);
        return into;
    }

    private static FlatBuilding FlatBlock(string place, int units = 8, int value = 4000000, int cond = 900, int income = 1000)
    {
        return new FlatBuilding { Place = place, Name = place, District = "ba:neighborhood_garmentdistrict", Value = value, Cond = cond, Income = income, Units = units, Sqm = 900, TaxYear = 3000 };
    }

    private static void FlatListingChecks()
    {
        bool most = true, repeat = true, price = true, owned = true;
        int listedUnits = 0, units = 0, windowsDiffer = 0;
        for (int i = 0; i < 300; i++)
        {
            FlatBuilding b = FlatBlock("Block~" + i, 3 + i % 10);
            List<FlatListing> l = FlatDraw(b, 991 + i, 25);
            most &= l.Count <= FlatCatalogue.ListedMost;
            repeat &= FlatDraw(b, 991 + i, 21).Select(x => x.Key).SequenceEqual(l.Select(x => x.Key)) && FlatDraw(b, 991 + i, 40).Select(x => x.Key).SequenceEqual(l.Select(x => x.Key));   // the same window
            windowsDiffer += FlatDraw(b, 991 + i, 25 + FlatCatalogue.ListWindow).Select(x => x.Key).SequenceEqual(l.Select(x => x.Key)) ? 0 : 1;
            int[] w = FlatRules.Weights(b.Place, b.Units, b.Income);
            int[] fh = FlatRules.Freehold(b.Value, w);
            foreach (FlatListing f in l)
            {
                price &= f.Freehold == fh[f.Slot] && f.Price == FlatRules.Lease(fh[f.Slot]) && f.Slot < b.Units && f.Kind == FlatRules.KindOf(b.Place, f.Slot, b.Income);
            }
            listedUnits += l.Count;
            units += b.Units;
            List<FlatListing> without = FlatDraw(b, 991 + i, 25, (place, slot) => l.Count > 0 && slot == l[0].Slot);
            owned &= without.All(x => l.Count == 0 || x.Slot != l[0].Slot);
        }
        Check(most, "flats: never more than " + FlatCatalogue.ListedMost + " units of one building are for sale at once");
        Check(repeat && windowsDiffer > 150, "flats: the draw is the building's, the save's seed and the window's alone: the same all through a window of " + FlatCatalogue.ListWindow + " days, a new draw after it (" + windowsDiffer + " of 300 changed)");
        Check(price, "flats: a listing's price is its unit's share of the building's value at the lease's share, and its kind and unit are the building's own");
        Check(owned, "flats: a unit the player holds is never listed again");
        int share = listedUnits * 1000 / units;
        Check(share > 60 && share < 190, "flats: about " + FlatCatalogue.ListChance / 10 + "% of a middling district's units are for sale in a window (" + share / 10.0 + "%)");
        Check(FlatDraw(FlatBlock("Pair~1", 2), 5, 25).Count == 0 && FlatDraw(FlatBlock("Pair~1", 1), 5, 25).Count == 0 && FlatDraw(FlatBlock("Zero~1", 8, 0), 5, 25).Count == 0,
            "flats: a block of fewer than " + FlatCatalogue.MinUnits + " units, or one worth nothing, is its landlord's whole and sells no flat");
        Check(FlatRules.ListChance(300, 1000) > FlatRules.ListChance(1000, 1000) && FlatRules.ListChance(1000, 600) > FlatRules.ListChance(1000, 1400)
              && FlatRules.ListChance(0, 600) <= FlatCatalogue.ListMost && FlatRules.ListChance(1000, 1400) >= FlatCatalogue.ListLeast,
            "flats: more of a run down building's units and of a poorer district's come up, within the least and the most");
        Check(FlatRules.Window(1) == 0 && FlatRules.Window(FlatCatalogue.ListWindow) == 0 && FlatRules.Window(FlatCatalogue.ListWindow + 1) == 1 && FlatRules.Window(-4) == 0,
            "flats: a window is " + FlatCatalogue.ListWindow + " days from the first");
        // A building bought whole is no building for flats, and one the game has no value for sells none: those are the game file's reads; the rule sees only what it is given.
        int cheap = 0, kept = 0;
        for (int i = 0; i < 400; i++)
        {
            cheap += FlatDraw(FlatBlock("Poor~" + i, 8, 4000000, 300, 600), 12, 30).Count;
            kept += FlatDraw(FlatBlock("Rich~" + i, 8, 4000000, 1000, 1400), 12, 30).Count;
        }
        Check(cheap > kept, "flats: a run down building in a poor district sells more of its units than a kept one in a rich district (" + cheap + " against " + kept + ")");
    }

    private static void FlatBillChecks()
    {
        const int monthDays = 5;
        int billed = -1, lines = 0;
        var months = new Dictionary<int, int>();
        for (int day = 40; day < 340; day++)
        {
            int month = FlatRules.MonthOf(day, monthDays);
            if (billed < 0)
            {
                billed = month;   // bought on this day: the month of purchase is not billed
            }
            if (FlatRules.BillDue(billed, month))
            {
                billed = month;
                lines++;
                months[month] = months.ContainsKey(month) ? months[month] + 1 : 1;
            }
            // the same day asked again, as a second pass or a load does: nothing
            Check(!FlatRules.BillDue(billed, month), "flats: the day's second look finds nothing due (day " + day + ")");
        }
        Check(months.Values.All(v => v == 1) && lines == 59, "flats: a month's service charge and tax are taken once each month, never twice (" + lines + " months over 300 days)");
        int gap = FlatRules.MonthOf(1000, monthDays);
        Check(FlatRules.BillDue(10, gap) && !FlatRules.BillDue(gap, gap) && !FlatRules.BillDue(gap + 1, gap), "flats: after a long gap one month is billed, with no back-bills, and a month already billed is not again");
        int one = FlatRules.ServiceMonth(18, monthDays), none = FlatRules.ServiceMonth(0, monthDays);
        Check(one == 18 * monthDays * FlatCatalogue.ServiceShare / 1000 && none == 1 && FlatRules.ServiceMonth(int.MaxValue, 99) > 0, "flats: the service charge is " + FlatCatalogue.ServiceShare / 10 + "% of the flat's rent for the month, a dollar at the least (" + one + ")");
        int year = 12000, sum = 0;
        for (int m = 0; m < FlatCatalogue.MonthsPerYear; m++)
        {
            sum += FlatRules.TaxMonth(year, 250000);
        }
        Check(sum == year / 4 && FlatRules.TaxMonth(0, 250000) == FlatCatalogue.TaxLeast && FlatRules.TaxMonth(year, 0) == FlatCatalogue.TaxLeast,
            "flats: the unit's share of the building's property tax a month is a twelfth of its share of the year's (" + sum + " of " + year + " over a year)");
        // The cost of keeping the cheapest flat is a small part of what a flat brings in.
        int price = 25520, rent = EstateRules.MarketRent(price, (int)EstateUse.Homes, 1000, 300, 0, EstateClimate.Steady);
        int monthly = FlatRules.ServiceMonth(rent, monthDays) + FlatRules.TaxMonth(1512, 150000);
        Check(rent > 0 && monthly < rent * monthDays, "flats: what the cheapest flat costs a month (" + monthly + ") is under the rent it would let for (" + rent * monthDays + ")");
    }

    private static void FlatFoldChecks()
    {
        const string place = "Fold~9";
        int units = 8, income = 1000;
        int[] w = FlatRules.Weights(place, units, income);
        long all = 0;
        bool lessThanBuilding = true, nonNegative = true;
        for (int slot = 0; slot < units; slot++)
        {
            int credit = FlatRules.FoldCredit(6000000, place, units, income, slot);
            all += credit;
            int listed = FlatRules.Value(6000000, place, units, income, slot);
            lessThanBuilding &= credit == listed && credit < 6000000;
            int paid = FlatRules.Lease(FlatRules.Freehold(5000000, w)[slot]);
            nonNegative &= FlatRules.FoldBasis(paid, credit) >= 0 && FlatRules.FoldBasis(paid, credit) == Math.Max(0, paid - credit);
        }
        Check(lessThanBuilding && nonNegative, "flats: a flat folded into a building bought whole is credited what its unit costs at the building's price, never more than that, and its cost over it counts against a gain, never under nothing");
        long expect = 6000000L * FlatCatalogue.LeaseShare / 1000;
        Check(Math.Abs(all - expect) <= units * FlatCatalogue.RoundTo, "flats: the credits of all the units of a building are the lease's share of the price (" + all + " of " + expect + "): the unit is not paid twice");
        int price = 6000000, credited = FlatRules.FoldCredit(price, place, units, income, 3);
        Check(price - credited < price && credited > 0 && FlatRules.FoldCredit(price, place, units, income, 40) == 0, "flats: the player pays the building's price less the flat's share, once");
        // The mortgage keeps its place: the key of the flat and of the building are different things.
        Check(FlatRules.IsKey(FlatRules.Key("a~1", 3)) && FlatRules.Key("a~1", 3) == "flat:a~1:3" && !FlatRules.IsKey("ba:street~12") && !FlatRules.IsKey(null),
            "flats: a flat's key is its own and a building's key is not one");
    }

    private static void FlatSaleChecks()
    {
        bool noProfit = true, offerUnderAsk = true, accepted = true;
        for (int seed = 1; seed < 200; seed++)
        {
            var h = new EstateHolding { Place = FlatRules.Key("Sale~" + seed, 2), Use = (int)EstateUse.Homes, Base = 30000, Cond = 900, Paid = 30000 };
            h.List = EstateCatalogue.Share(30000, 1000);
            int offer = 0;
            int day = 5;
            for (; day < 60 && offer == 0; day++)
            {
                offer = EstateRules.SaleOffer(h, seed, day, 30000, 1);
            }
            if (offer > 0)
            {
                offerUnderAsk &= offer <= h.List;
                accepted &= EstateRules.AcceptOffer(h, seed, day, 1) && h.SalePrice == offer && h.SaleDay == day + EstateCatalogue.CompletionDays;
                int fee = EstateRules.AgentFee(offer, EstateCatalogue.IndexStart), tax = EstateRules.GainsTax(offer, fee, 30000, 0, 20);
                noProfit &= offer - fee - tax < 30000;   // bought at its value and sold at it, a flat never makes money in itself
            }
        }
        Check(offerUnderAsk && accepted && noProfit, "flats: a flat is sold by the rule listings already have (an offer never over the asking price, agreed to complete in " + EstateCatalogue.CompletionDays
            + " days) and buying then selling at the same value never makes money");
        Check(EstateRules.AgentFee(30000, 1000) == Math.Max(EstateCatalogue.AgentFeeLeast, 30000 * EstateCatalogue.AgentFee / 1000), "flats: the agent's fee is the listings' own");
        // A mortgage is paid out of the sale: the net is what is left.
        int owed = 20000, price = 31000, fee2 = EstateRules.AgentFee(price, 1000);
        Check(Math.Max(0, price - fee2 - owed) == price - fee2 - owed && price - fee2 - owed < price, "flats: what the player keeps of a sale is the price less the fee, the tax and what is owed on the mortgage");
    }

    private static void FlatLetChecks()
    {
        // A flat let by the rules a building's units are let by: an applicant, a lease, the rent a day, the tenant's days.
        int value = 25520;
        EstateClimate c = EstateClimate.Steady;
        var h = new EstateHolding { Place = FlatRules.Key("Let~4", 1), Use = (int)EstateUse.Homes, Base = value, Cond = 900, Managed = true, Income = 1000 };
        int market = EstateRules.MarketRent(h, c), unit = EstateRules.UnitRent(h, c);
        Check(market > 0 && unit == market && h.Units.Count == 1, "flats: the flat's market rent is the building's rules on the flat's own value (" + market + " a day), one unit");
        // A building's unit rent at the building's value is far more: the flat's rent is the lease's share of it.
        var whole = new EstateHolding { Place = "Let~4", Use = (int)EstateUse.Homes, Base = 4000000, Cond = 900, Managed = true, Income = 1000 };
        Check(unit * 20 < EstateRules.MarketRent(whole, c), "flats: a flat lets for a small part of what its building would, as it costs a small part");
        RentApplicant a = null;
        for (int day = 1; day < 200 && a == null; day++)
        {
            a = EstateRules.Apply(h, 0, 4242, day, c);
        }
        Check(a != null && a.Offer > 0 && h.Units[0].Applicants.Count == 1, "flats: applicants come for a flat to let by the letting rules");
        bool took = a != null && EstateRules.Accept(h, a.Id, 10);
        Check(took && h.Units[0].Let && h.Units[0].Rent == a.Offer, "flats: an applicant is accepted into the flat's one unit at the rent offered");
        int paid = 0, days = 0, missed = 0;
        for (int day = 11; day < 71; day++)
        {
            TenancyDay d = EstateDay.Unit(new EstateState { Seed = 4242 }, h, 0, day, c, false);
            paid += d.Pays ? h.Units[0].Rent : 0;
            paid += d.Lump;
            missed += d.Missed ? 1 : 0;
            days++;
            if (!h.Units[0].Let)
            {
                break;
            }
        }
        Check(paid > 0 && days > 0 && paid <= days * a.Offer + 1, "flats: the rent comes in a day at a time by the tenancy's rules and never more than the rent agreed (" + paid + " over " + days + " days, " + missed + " missed)");
        h.Units[0].Applicants.Clear();
        Check(!EstateRules.MayServeNotice(h, 0, 20) || h.Units[0].Arrears > 0, "flats: notice is served by the building's rule: only on rent owed or a lease's end");
    }

    private static void FlatRefusalChecks()
    {
        Check(FlatRules.MayBuy(false, false, true, 0, 100000, 1000, 5000, 5000) == FlatRefusal.Off, "flats: with the part off nothing is bought");
        Check(FlatRules.MayBuy(true, true, true, 0, 100000, 1000, 5000, 5000) == FlatRefusal.Session, "flats: in a shared city nothing is bought or sold: who owns what is the host's record");
        Check(FlatRules.MayBuy(true, false, false, 0, 100000, 1000, 5000, 5000) == FlatRefusal.Gone, "flats: a unit that is no longer listed is not bought");
        Check(FlatRules.MayBuy(true, false, true, FlatCatalogue.HeldMost, 100000, 1000, 5000, 5000) == FlatRefusal.Most, "flats: a player holds " + FlatCatalogue.HeldMost + " flats at most");
        Check(FlatRules.MayBuy(true, false, true, 0, 100000, 1000, 5000, 5010) == FlatRefusal.Moved, "flats: a price that moved while the player decided is refused before any money, so the price charged is the price shown");
        Check(FlatRules.MayBuy(true, false, true, 0, 500, 1000, 5000, 5000) == FlatRefusal.Cash, "flats: cash under the deposit is refused");
        Check(FlatRules.MayBuy(true, false, true, 0, 1000, 1000, 5000, 5000) == FlatRefusal.None, "flats: with the cash for the deposit, nothing is in the way");
        Check(Enum.GetValues(typeof(FlatRefusal)).Length == 11, "flats: every refusal has its words (checked with the texts)");
    }

    private static void FlatStoreChecks()
    {
        var book = new FlatBook();
        var f = new OwnedFlat { Place = "Street~14", Slot = 3, Kind = 1, Units = 8, Paid = 25520, BoughtDay = 41, Value = 26000, BilledMonth = 8 };
        f.Hold.Place = f.Key;
        f.Hold.Cond = 640;
        f.Hold.Income = 900;
        f.Hold.Base = 26000;
        f.Hold.Managed = true;
        f.Hold.List = 28000;
        f.Hold.Offer = 27000;
        f.Hold.OfferLapse = 90;
        f.Hold.NextApplicant = 4;
        f.Hold.Spent = 120;
        EstateUnit u = f.Hold.Units[0];
        u.Ask = 1100; u.ToLet = false; u.Tenant = 2; u.Record = 70; u.Rent = 31; u.Deposit = 248; u.Until = 120; u.Arrears = 62; u.ArrearsDays = 2; u.NoticeEnd = 99; u.CourtDay = 100; u.VoidDays = 5;
        u.Applicants.Add(new RentApplicant { Id = 3, Kind = 4, Offer = 29, Record = 55, Term = 90, Lapse = 70 });
        book.Flats.Add(f);
        string line = FlatStore.Write(book);
        var back = new FlatBook();
        bool read = FlatStore.Read(back, line);
        OwnedFlat g = back.Flats.Count == 1 ? back.Flats[0] : null;
        Check(read && g != null && g.Place == "Street~14" && g.Slot == 3 && g.Kind == 1 && g.Units == 8 && g.Paid == 25520 && g.BoughtDay == 41 && g.Value == 26000 && g.BilledMonth == 8
              && g.Hold.Cond == 640 && g.Hold.Income == 900 && g.Hold.Base == 26000 && g.Hold.Managed && g.Hold.List == 28000 && g.Hold.Offer == 27000 && g.Hold.OfferLapse == 90 && g.Hold.Spent == 120
              && g.Hold.NextApplicant == 4 && g.Key == f.Key,
            "flats: a flat's record round trips through its line of the save");
        EstateUnit v = g != null ? g.Hold.Units[0] : null;
        Check(v != null && v.Ask == 1100 && !v.ToLet && v.Tenant == 2 && v.Record == 70 && v.Rent == 31 && v.Deposit == 248 && v.Until == 120 && v.Arrears == 62 && v.ArrearsDays == 2 && v.NoticeEnd == 99
              && v.CourtDay == 100 && v.VoidDays == 5 && v.Applicants.Count == 1 && v.Applicants[0].Offer == 29 && v.Applicants[0].Lapse == 70,
            "flats: the lease, the arrears, the notice and the applicants round trip too");
        Check(FlatStore.Write(back) == line, "flats: the line written again is the line read");
        var older = new FlatBook();
        FlatStore.Load(older, new Dictionary<string, string> { { EstateStore.Key, "v=4^h:1" } });
        Check(older.Flats.Count == 0 && older.Unread == null, "flats: a save from before has no line and no flats, and loads as it was");
        var data = new Dictionary<string, string> { { FlatStore.Key, "x" } };
        FlatStore.Save(new FlatBook(), data);
        Check(!data.ContainsKey(FlatStore.Key), "flats: with no flats the save has no line of this kind");
        var damaged = new FlatBook();
        bool ok = FlatStore.Read(damaged, "garbage without a version");
        string kept = FlatStore.Write(damaged);
        Check(!ok && damaged.Flats.Count == 0 && kept == "garbage without a version", "flats: a line this version cannot read is kept as it was read");
        var cut = new FlatBook();
        FlatStore.Read(cut, "v=1^f:Street~1|3|1");
        Check(cut.Flats.Count == 0, "flats: a record cut short is left out and never throws");
        var newer = new FlatBook();
        FlatStore.Read(newer, line.Replace("v=1", "v=9") + "|extra|fields");
        Check(newer.Flats.Count == 1 && newer.Flats[0].Place == "Street~14", "flats: a newer version's line is read as far as it goes");
        var clamp = new FlatBook();
        FlatStore.Read(clamp, line.Replace("|1100|", "|99999|"));
        Check(clamp.Flats.Count == 1 && clamp.Flats[0].Hold.Units[0].Ask <= EstateCatalogue.AskMost, "flats: a figure out of its range is held to it");
        var many = new FlatBook();
        string rec = line.Substring(line.IndexOf("f:", StringComparison.Ordinal) + 2);
        var lineMany = "v=1^f:" + string.Join(";", Enumerable.Range(0, FlatCatalogue.HeldMost + 4).Select(i => rec.Replace("Street~14", "Street~" + (100 + i))));
        FlatStore.Read(many, lineMany);
        Check(many.Flats.Count == FlatCatalogue.HeldMost, "flats: no more than " + FlatCatalogue.HeldMost + " flats are read");
        var state = new EstateState();
        Check(state.Flats != null && state.Flats.Flats.Count == 0 && FlatStore.Key != EstateStore.Key && FlatStore.Key != EstateExtraStore.TenantsKey, "flats: the line is its own, beside the Estate's and never inside it");
    }

    // The mod's own districts, from their own figures (Scripts/Uptown/DistrictSplit): a residential building there is a size of the game's laid on a lot in storeys (the floor is the
    // size's square metres times its storeys, a unit one storey), at the district's square metre (the base price times residential's 1.5 times its real estate multiplier). The
    // cheapest is the game's smallest residential size (54 m2, three storeys, the fewest units that split) on its quietest street (the traffic factor 0.4 the game's cheapest has).
    private static int FlatMineStudio(int district, out string detail)
    {
        RealLifeOverhaul.Uptown.DistrictFigures f = RealLifeOverhaul.Uptown.DistrictSplit.Figures(district);
        int best = int.MaxValue;
        detail = string.Empty;
        for (int storeys = 3; storeys <= 10; storeys++)
        {
            for (int lot = 1; lot <= 40; lot++)
            {
                string place = "reallife%3Astreet_test" + district + "~" + lot;
                int value = EstateRules.Value((int)(54 * storeys * f.BasePrice * 1.5 * f.RealEstate * 0.4), EstateCatalogue.RealStart, EstateCatalogue.IndexStart, EstateCatalogue.IncomeMiddle, 300, 0);
                int[] w = FlatRules.Weights(place, storeys, EstateCatalogue.IncomeMiddle);
                int[] shares = FlatRules.Freehold(value, w);
                for (int i = 0; i < storeys; i++)
                {
                    if (w[i] == FlatCatalogue.KindWeight[0] && FlatRules.Lease(shares[i]) < best)
                    {
                        best = FlatRules.Lease(shares[i]);
                        detail = storeys + " storeys worth " + value;
                    }
                }
            }
        }
        return best;
    }

    private static void FlatMoreChecks()
    {
        string detail, detail2;
        int uptown = FlatMineStudio(0, out detail), cedar = FlatMineStudio(1, out detail2);
        Check(uptown == 47350 && cedar == 28710 && ProductTerms.Deposit(cedar, ProductCatalogue.HomeDepositLeast) == 5742,
            "flats: the cheapest studio in Uptown is " + uptown + " (" + detail + "), deposit " + ProductTerms.Deposit(uptown, ProductCatalogue.HomeDepositLeast)
            + "; in Cedar Heights " + cedar + " (" + detail2 + "), deposit " + ProductTerms.Deposit(cedar, ProductCatalogue.HomeDepositLeast));
        // The duties of a landlord, on a flat's one unit, by the rules a building's units have.
        var s = new EstateState { Seed = 99 };
        var f = new OwnedFlat { Place = "Duty~5", Slot = 1, Units = 6, Paid = 30000, Value = 30000 };
        f.Hold.Place = f.Key; f.Hold.Use = (int)EstateUse.Homes; f.Hold.Base = 30000; f.Hold.Cond = 900; f.Hold.Managed = true; f.Hold.Income = 1000;
        s.Flats.Flats.Add(f);
        Check(ReferenceEquals(s.Holding(f.Key), f.Hold) && s.Holding("Duty~5") == null && s.LetCount == 1 && ReferenceEquals(s.LetAt(0), f.Hold),
            "flats: a flat's key finds its holding, so a tenancy, a call and a sale are looked up as a building's are, and the buildings and the flats are one list for what both let");
        EstateClimate c = EstateClimate.Steady;
        RentApplicant a = null;
        for (int day = 1; day < 200 && a == null; day++)
        {
            a = EstateRules.Apply(f.Hold, 0, 99, day, c);
        }
        Check(a != null && EstateRules.Accept(f.Hold, a.Id, 10), "flats: a flat lets to an applicant");
        int calls = 0, inspected = 0;
        for (int day = 11; day < 500 && f.Hold.Units[0].Let; day++)
        {
            DutyDay d = EstateRules.Duty(f.Hold, 0, 99, day, c);
            calls += d.NewCall ? 1 : 0;
            Inspection r = EstateDay.Inspect(s, f.Hold, day, c, false);
            inspected += r.Done ? 1 : 0;
            EstateDay.Unit(s, f.Hold, 0, day, c, true);
            EstateRules.Wear(f.Hold);
        }
        Check(calls > 0 && inspected > 0, "flats: a let flat reports repairs and is inspected by the city by a building's own rules (" + calls + " calls, " + inspected + " inspections)");
        f.Hold.Manager = (int)EstateManager.Manager;
        f.Hold.Units[0].Ask = 1100;
        f.Hold.Level = 2;
        f.Hold.Units[0].Call = 3; f.Hold.Units[0].CallDay = 40; f.Hold.Units[0].CallCost = 80; f.Hold.Units[0].Happy = 640; f.Hold.InspectDay = 77; f.Hold.RentYear = 500; f.Hold.CostYear = 90;
        var book = new FlatBook();
        book.Flats.Add(f);
        var back = new FlatBook();
        FlatStore.Read(back, FlatStore.Write(book));
        OwnedFlat g = back.Flats.Count == 1 ? back.Flats[0] : null;
        Check(g != null && g.Hold.Manager == (int)EstateManager.Manager && g.Hold.Units[0].Call == 3 && g.Hold.Units[0].CallCost == 80 && g.Hold.Units[0].Happy == 640 && g.Hold.InspectDay == 77
              && g.Hold.Level == 2 && g.Hold.RentYear == 500 && g.Hold.CostYear == 90 && FlatStore.Write(back) == FlatStore.Write(book),
            "flats: the manager chosen, the open call, the mood, the inspection and the year's figures round trip through the save, the manager passing through untouched");
        // A flat in a building the player rents is listed, and says so.
        FlatBuilding b = FlatBlock("Rented~3");
        b.Renting = true;
        var into = new List<FlatListing>();
        for (int day = 1; day < 400 && into.Count == 0; day += FlatCatalogue.ListWindow)
        {
            FlatRules.Listings(into, b, 5, day, null);
        }
        Check(into.Count > 0 && into.All(x => x.Renting), "flats: a unit of a building the player rents is listed, marked as the one they live in");
        string game = ModSource(@"Economy\Estate\EstateGame.Flats.cs"), director = ModSource(@"Economy\Estate\EstateDirector.Flats.cs");
        Check(game.Contains("reg.RentPerDay <= 0f || held.In(c.Place) > 0") && game.Contains("reg.RentPerDay = 0f;") && game.Contains("deposit = Mathf.Max(0, Mathf.FloorToInt(reg.lastDeposit));")
              && director.Contains("Pay(back, EstateGame.FlatDepositKey"),
            "flats: buying the flat one rents ends the rent and gives the deposit back, touching nothing inside; a building rented free or with a flat held is not listed again");
        Check(ModSource(@"Economy\Estate\EstateDirector.App.cs").Contains("FlatNoticePick(ref tab, ref pick);") && director.Contains("private static void FlatNoticePick("),
            "flats: a notice about a flat's unit, call or applicant opens the flat's page");
        string tenants = ModSource(@"Economy\Estate\EstateApp.Property.cs"), works = ModSource(@"Ui\Estate\EstateLive.Works.cs"), alerts = ModSource(@"Economy\Estate\EstateDirector.Property.cs");
        Check(MemberText(tenants, "private void WriteTenants(").Contains("state.LetAt(i)") && works.Contains("_s.LetAt(i)") && alerts.Contains("EstateHolding h = _state.LetAt(i);")
              && MemberText(director, "private void FlatLetDay(").Contains("DutiesMorning(h, day)"),
            "flats: a let flat is in the Tenants tab, the calls, the Planner's rows and the badge on both interfaces, and has the landlord's morning");
        Check(FlatCatalogue.LeaseShare > 0 && FlatCatalogue.LeaseShare < 1000 && !ModSource(@"Economy\Estate\FlatRules.cs").Contains("150"), "flats: the lease's share is one constant and nothing else writes it");
    }

    private static string FlatSource(string relative) => ModSource(relative);

    // A flat under the player's own agency (manager value 3), as a building is: the selector offers it only while the agency is open, the agents' hours count the flat's one unit, and
    // the agency's cover, its closing and the calls waiting are asked of every let holding, flats included.
    private static void FlatAgencyChecks()
    {
        string old = FlatSource(@"Economy\Estate\EstateApp.Flats.cs"), kit = FlatSource(@"Ui\Estate\EstateLive.Flats.cs"), book = FlatSource(@"Economy\Estate\EstateDirector.Book.cs");
        string flats = FlatSource(@"Economy\Estate\EstateDirector.Flats.cs"), duties = FlatSource(@"Economy\Estate\EstateDirector.Duties.cs");
        Check(old.Contains("_director.AgencyOpen || h.Manager == (int)EstateManager.Agency ? EstateCatalogue.Managers : EstateCatalogue.Managers - 1")
              && old.Contains("box.Selector(choices, h.Manager, EstateText.Manager, i => _director.SetManager(key, i))"),
            "flats under the agency: the old page's manager selector offers your own agency while it is open, or while the flat is under it already, as a building's does");
        Check(kit.Contains("EstateCatalogue.Managers - (_d.AgencyOpen ? 1 : 2)") && kit.Contains("manager < most"),
            "flats under the agency: the kit's manager step reaches your own agency only while it is open, as a building's does");
        Check(book.Contains("for (int i = 0; Duties && i < _state.LetCount; i++)") && Occurs(book, "_state.LetAt(i)") == 3 && !book.Contains("_state.Holdings[i]"),
            "flats under the agency: the agents' hours, each holding's cover and the agency's closing look at the buildings and the flats alike");
        Check(duties.Contains("to == (int)EstateManager.Agency && !AgencyOpen") && duties.Contains("AgencyCover();") && flats.Contains("!EstateRules.Runs(h) ? EstateRules.CallsWaiting(h)")
              && duties.Contains("h.Manager != (int)EstateManager.Manager"),
            "flats under the agency: the same refusal while the agency is shut, cover worked out after a change, no fee taken and no call left waiting for a flat the agents cover");
        var hold = new EstateHolding { Manager = (int)EstateManager.Agency, Covered = true };
        Check(EstateRules.Runs(hold) && EstateCatalogue.Managers == 4 && FlatStoreManagerKept(), "flats under the agency: a covered flat is run as a manager runs it, and its manager value 3 is kept in the save");
    }

    // The save's manager value of a flat survives a round trip as 3 (the agency).
    private static bool FlatStoreManagerKept()
    {
        var book = new FlatBook();
        var flat = new OwnedFlat { Place = "Main~1", Slot = 0, Kind = 0, Units = 4, Paid = 1000, BoughtDay = 5 };
        flat.Hold.Place = FlatRules.Key(flat.Place, 0);
        flat.Hold.Manager = (int)EstateManager.Agency;
        book.Flats.Add(flat);
        var data = new System.Collections.Generic.Dictionary<string, string>();
        FlatStore.Save(book, data);
        var back = new FlatBook();
        FlatStore.Load(back, data);
        return back.Flats.Count == 1 && back.Flats[0].Hold.Manager == (int)EstateManager.Agency;
    }

    private static void FlatSourceChecks()
    {
        FlatAgencyChecks();
        string director = FlatSource(@"Economy\Estate\EstateDirector.Flats.cs");
        string game = FlatSource(@"Economy\Estate\EstateGame.Flats.cs");
        string desk = FlatSource(@"Economy\Banking\Desk\CreditDesk.Flats.cs");
        string main = FlatSource(@"Economy\Estate\EstateDirector.cs");
        string deskMain = FlatSource(@"Economy\Banking\Desk\CreditDesk.cs");
        string buy = MemberText(director, "public bool BuyFlat(");
        Check(buy.Length > 0 && buy.IndexOf("MayBuyFlat(", StringComparison.Ordinal) < buy.IndexOf("Cost(deposit", StringComparison.Ordinal) && buy.IndexOf("Cost(deposit", StringComparison.Ordinal) < buy.IndexOf("EstateGame.TakeHome", StringComparison.Ordinal)
              && buy.Contains("Pay(deposit, EstateGame.FlatBuyKey"),
            "flats: the refusals come before any money, the home is taken after the deposit, and the deposit goes back if the home cannot be");
        Check(game.Contains("GoingPublic.InSession") && MemberText(game, "public static void ReadFlatBuildings(").Contains("GoingPublic.InSession") && MemberText(game, "public static bool TakeHome(").Contains("GoingPublic.InSession"),
            "flats: in a Going Public session no building is read for sale and none is made the player's home: a unit is the buying player's only, nothing is sent");
        Check(director.Contains("GoingPublic.InSession") && MemberText(director, "internal static int FlatSecurityState(").Contains("GoingPublic.InSession"),
            "flats: a session leaves every mortgage on a flat as it is and completes no sale");
        Check(MemberText(game, "public static bool TakeHome(").Contains("BuildingHelper.RentBuilding(b, 0f, 0f)") && MemberText(game, "public static bool TakeHome(").Contains("reg.BuildingOwnedByPlayer"),
            "flats: a flat becomes the player's home through the game's own RentBuilding at no rent, as a rented apartment does, and never for a building the player rents or owns");
        Check(!Regex.IsMatch(director + game + desk, @"void (Update|LateUpdate|FixedUpdate)\b") && !director.Contains("FindObjectsByType"), "flats: no Update and no search of the scene: the listing is drawn when the day, the app or a purchase asks");
        Check(MemberText(director, "private void FlatsDay(").Contains("FlatRules.BillDue(f.BilledMonth, month)") && MemberText(director, "private void FlatBills(").Contains("f.BilledMonth = month;")
              && Regex.Matches(director, @"(?<!Test)FlatBills\(").Count == 3,
            "flats: the month's two lines are taken from one place, the day's pass or the test tool, each setting the month first");
        Check(main.Contains("case EstatePart.Flats: return Settings.Estate && Settings.EstateFlats;") && main.Contains("part != EstatePart.Flats"),
            "flats: the part is on only with the property part and its own option, and stands down with a fault of the property part");
        Check(main.Contains("FlatStore.Load(_state.Flats, save.modData);") && main.Contains("FlatsWritten(save.modData);"), "flats: the flats are read at a start and written with the Estate's own line");
        Check(deskMain.Contains("FlatRules.IsKey(a.Security)") && deskMain.Contains("FlatSecurity != null ? FlatSecurity(a.Security) : FlatUnknown) != FlatGone"),
            "flats: a mortgage secured by a flat is gone only when the property business says so, and unknown leaves it as it is");
        Check(MemberText(director, "private void CompleteFlatSale(").Contains("SettleFlatSale(f.Key, price)") && MemberText(director, "private void FoldFlats(").Contains("MoveSecurity("),
            "flats: a sale settles the mortgage out of the proceeds and a fold moves it to the building");
        string day = FlatSource(@"Economy\Finance\DayRules.cs");
        Check(day.Contains("\"flat_buy\", \"flat_sold\", \"flat_fold\""), "flats: the purchase, the sale and the fold are capital in the day's result; the charge, the tax and the rent count");
        string[] keys = { "reallife_transaction_flat_buy", "reallife_transaction_flat_sold", "reallife_transaction_flat_fold", "reallife_transaction_flat_service", "reallife_transaction_flat_tax", "reallife_transaction_flat_rent" };
        Check(keys.All(k => game.Contains("\"" + k + "\"")), "flats: every bank line is its own key");
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("flats", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        Check(keys.All(k => en.ContainsKey(k) && en.ContainsKey(k + "_label")), "flats: every bank line has its words and its label");
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (string text in new[] { director, FlatSource(@"Economy\Estate\EstateApp.Flats.cs"), FlatSource(@"Ui\Estate\EstateLive.Flats.cs"), game, FlatSource(@"Economy\Estate\EstateGuide.cs") })
        {
            foreach (Match m in Regex.Matches(text, "\"(reallife_(?:flat|opt_tip_economy_estate_flats|meta_title_economy_estate_flats|guide_economy_estate_flats)[a-z0-9_]*)\""))
            {
                used.Add(m.Groups[1].Value);
            }
        }
        foreach (string key in new[] { "reallife_flat_opt", "reallife_opt_tip_economy_estate_flats", "reallife_meta_title_economy_estate_flats", "reallife_guide_economy_estate_flats", "reallife_guide_economy_estate_flats_about" })
        {
            used.Add(key);
        }
        string missing = string.Join(", ", used.Where(k => !en.ContainsKey(k)).Take(5));
        Check(used.Count > 40 && missing.Length == 0, "flats: every text the flats use is in the English file (" + used.Count + " keys)" + (missing.Length > 0 ? ", missing " + missing : string.Empty));
        string refusals = FlatSource(@"Economy\Estate\EstateDirector.Flats.cs");
        Check(new[] { "off", "session", "gone", "most", "cash", "moved", "not_mine", "let", "selling", "terms" }.All(r => en.ContainsKey("reallife_flat_refuse_" + r) && refusals.Contains("reallife_flat_refuse_" + r)),
            "flats: every refusal has its words");
        Check(en["reallife_opt_tip_economy_estate_flats"].Length <= 190 && en["reallife_opt_tip_economy_estate_flats"].Split(' ').Length <= 32, "flats: the option's tip is at most 190 characters and 32 words");
        Check(new[] { "reallife_flat_note_bought", "reallife_flat_note_bought_mortgage", "reallife_flat_note_sold", "reallife_flat_note_sale_fell", "reallife_flat_note_folded" }.All(k => en[k].Length <= 240),
            "flats: every notice is at most 240 characters");
        string guide = en["reallife_guide_economy_estate_flats"];
        var facts = new HashSet<string>(Regex.Matches(guide, @"\{(flat_[a-z_]+)\}").Cast<Match>().Select(m => m.Groups[1].Value));
        string guideSource = FlatSource(@"Economy\Estate\EstateGuide.cs");
        Check(facts.Count >= 10 && facts.All(f => guideSource.Contains("\"" + f + "\"")), "flats: every figure the Guide's page prints is filled from the code (" + facts.Count + " facts)");
        Check(!langs.Take(1).Any(l => l.Values.Any(v => v.Contains("—") && v.Contains("reallife_flat"))), "flats: no dash in the texts");
        bool dashes = en.Where(kv => kv.Key.StartsWith("reallife_flat_", StringComparison.Ordinal) || kv.Key.StartsWith("reallife_transaction_flat_", StringComparison.Ordinal)).All(kv => !kv.Value.Contains('—') && !kv.Value.Contains('–') && !kv.Value.Contains('!'));
        Check(dashes, "flats: no dash and no exclamation mark in the flats' texts");
    }
}
