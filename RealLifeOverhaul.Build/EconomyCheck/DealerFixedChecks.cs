using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Dealership;

// Fixed prices at a Car Showroom: a switch for each showroom, saved with it (a line without it loads with it off); a buyer
// there offers the asking price and the staff close at it with no haggling; fewer buyers make an offer, sized so a fair price
// still sells; switched on, the offers below the asking price are withdrawn; the app, the self-check and the words.
internal static partial class Program
{
    private static void DealerFixedPrices()
    {
        DealerFixedRules();
        DealerFixedSave();
        DealerFixedBalance();
        DealerFixedSources();
        DealerFixedWords();
    }

    private static void DealerFixedRules()
    {
        Check(DealerRules.FixedChance(1000, true) == DealerCatalogue.FixedOffers && DealerRules.FixedChance(240, true) == 240 * DealerCatalogue.FixedOffers / 1000
              && DealerRules.FixedChance(240, false) == 240 && DealerRules.FixedChance(-5, true) == 0 && DealerCatalogue.FixedOffers < 1000 - DealerCatalogue.ProfileShare[0],
            "dealer fixed: with fixed prices " + DealerCatalogue.FixedOffers + " thousandths of the usual offers are made, fewer than the buyers who are not bargain hunters, and nothing changes without them");
        var y = BizYard("Fx~1", 2, 1000);
        var car = y.Cars[0];
        var d = new DealerOffer { Id = 1, Car = car.Id, Profile = 0, Offer = 18000, Ceiling = 19500 };
        DealerRules.AtAsking(d, car);
        Check(d.Offer == car.Asking && d.Ceiling == car.Asking && d.Profile == 1, "dealer fixed: a buyer there offers the asking price, goes no higher, and buys as an ordinary buyer");
        var low = new DealerOffer { Id = 2, Car = car.Id, Offer = 18000, Ceiling = 20000 };
        Check(DealerRules.Price(y, low, car) == 18000 && !DealerRules.StaffClose(y, low, car), "dealer fixed: off, a deal closes at the buyer's offer and the staff wait for their floor");
        y.Fixed = true;
        car.Asking = 22000;
        Check(DealerRules.Price(y, low, car) == 22000 && DealerRules.StaffClose(y, low, car), "dealer fixed: on, a deal closes at the asking price as it stands, and the staff close it at once");
        var drive = new DealerOffer { Id = 3, Car = car.Id, Offer = 22000, Ceiling = 22000, WantsDrive = true };
        bool before = DealerRules.StaffClose(y, drive, car);
        drive.Drove = true;
        Check(!before && DealerRules.StaffClose(y, drive, car), "dealer fixed: a buyer who asked for a test drive still has it before the staff close");
        y.Deals.Add(low);
        y.Deals.Add(new DealerOffer { Id = 4, Car = car.Id, Offer = 22000, Ceiling = 22000 });
        y.Deals.Add(new DealerOffer { Id = 5, Car = 999, Offer = 1, Ceiling = 1 });
        int gone = DealerRules.Withdraw(y);
        Check(gone == 1 && y.Deal(2) == null && y.Deal(4) != null && y.Deal(5) != null,
            "dealer fixed: switched on, only the offers below the asking price are withdrawn (an offer whose car is gone is left to the hour)");

        // An hour with fixed prices: every offer at the asking price, fewer of them, and the same on every machine.
        int fixedOffers = 0, plainOffers = 0;
        bool atAsking = true, same = true;
        for (int seed = 1; seed <= 40; seed++)
        {
            int a = DealerFixedHours(seed, true, out bool asked, out string first);
            DealerFixedHours(seed, true, out bool _, out string second);
            fixedOffers += a;
            plainOffers += DealerFixedHours(seed, false, out bool _, out string _);
            atAsking &= asked;
            same &= first == second;
        }
        Check(atAsking && fixedOffers > 0, "dealer fixed: every offer made at a showroom with fixed prices is at the asking price, with no room above it");
        Check(same, "dealer fixed: the same showroom, seed and hours give the same offers on every machine (the showroom's own dice only)");
        int share = plainOffers > 0 ? (int)(fixedOffers * 1000L / plainOffers) : 0;
        Check(share >= DealerCatalogue.FixedOffers - 120 && share <= DealerCatalogue.FixedOffers + 120,
            "dealer fixed: about " + DealerCatalogue.FixedOffers + " thousandths of the usual offers are made with fixed prices (" + fixedOffers + " against " + plainOffers + ", " + share + ")");
    }

    // A day of a busy showroom's hours; the offers made, whether all were at the asking price, and the line written.
    private static int DealerFixedHours(int seed, bool fixedPrices, out bool atAsking, out string line)
    {
        var s = new DealerState { Seed = seed };
        var y = BizYard("Fh~" + seed, 6, 1000);
        y.Fixed = fixedPrices;
        s.Yards.Add(y);
        var worths = new List<int> { 20000, 20000, 20000, 20000, 20000, 20000 };
        var hour = new YardHour { Open = true, ShopCustomers = 60f, Promotion = 80, BaseMultiplier = 0.5f, Satisfaction = 60, Staff = 6, Skill = 60 };
        int offers = 0;
        atAsking = true;
        var written = new System.Text.StringBuilder();
        for (int h = 9; h < 19; h++)
        {
            offers += DealerRules.Hour(s, y, hour, 24 + h, 1, worths, 5000, BizModels, DealerColours, 1000).Offers;
            for (int i = 0; i < y.Deals.Count; i++)
            {
                StockCar car = y.Car(y.Deals[i].Car);
                atAsking &= !fixedPrices || car != null && y.Deals[i].Offer == car.Asking && y.Deals[i].Ceiling == car.Asking;
            }
            written.Append(DealerStore.Write(s));
            y.Deals.Clear();   // each hour's offers looked at and answered, so no showroom's cap of offers waiting skews the count
        }
        line = written.ToString();
        return offers;
    }

    private static void DealerFixedSave()
    {
        var s = new DealerState { Seed = 5 };
        var on = BizYard("Sv~1", 1, 1000);
        on.Fixed = true;
        on.FixedSold = 3;
        var off = BizYard("Sv~2", 1, 1000);
        s.Yards.Add(on);
        s.Yards.Add(off);
        string line = DealerStore.Write(s);
        DealerState back = DealerStore.Read(line, out int _);
        Check(back != null && back.Yards.Count == 2 && back.Yards[0].Fixed && !back.Yards[1].Fixed && back.Yards[0].FixedSold == 0 && DealerStore.Write(back) == line,
            "dealer fixed: the switch is saved with each showroom and reads back (the day's check counts are not saved)");
        // The line as a build before fixed prices wrote it: the showroom's record ends at its events' days.
        string older = line.Replace("|1^t:", "^t:").Replace("|0^t:", "^t:");
        DealerState old = DealerStore.Read(older, out int _);
        Check(older != line && old != null && old.Yards.Count == 2 && !old.Yards[0].Fixed && !old.Yards[1].Fixed && old.Yards[0].EventLast[DealerCatalogue.EventKinds - 1] == -1,
            "dealer fixed: a save from before fixed prices loads with them off, every other field as it was");
    }

    // The reference showroom of DealerBalance with fixed prices, against the same showroom played as DealerBalance plays it
    // (the staff close at their floor, the player counters the rest once at the asking price): a fair asking price still sells,
    // and the full price on fewer sales comes to about what careful haggling makes, so neither way is free money.
    private static void DealerFixedBalance()
    {
        long plainMargin = DealerFixedMonth(false, out int plainSold, out bool _);
        long fixedMargin = DealerFixedMonth(true, out int fixedSold, out bool allAtAsking);
        int days = 30;
        Console.WriteLine("  dealer fixed: the reference showroom sold " + fixedSold + " cars for " + fixedMargin / days + " a day with fixed prices, " + plainSold + " for "
                          + plainMargin / days + " a day haggling.");
        Check(allAtAsking, "dealer fixed: every car the reference showroom sold with fixed prices went at its asking price");
        Check(fixedSold >= days / 3 && fixedSold < plainSold, "dealer fixed: a fair asking price still sells (" + fixedSold + " in " + days + " days), fewer than with haggling (" + plainSold + ")");
        Check(fixedMargin * 100 >= plainMargin * 80 && fixedMargin * 100 <= plainMargin * 110,
            "dealer fixed: fewer sales at the full price make between 80% and 110% of the haggling margin (" + fixedMargin + " against " + plainMargin + ")");
    }

    private static long DealerFixedMonth(bool fixedPrices, out int sold, out bool allAtAsking)
    {
        var s = new DealerState { Seed = 2026 };
        var y = new DealerYard { Place = "Ref~1", Fixed = fixedPrices };
        s.Yards.Add(y);
        var hour = new YardHour { Open = true, ShopCustomers = 25f, Promotion = 50, BaseMultiplier = 0.5f, Satisfaction = 50, Staff = 2, Skill = 50 };
        var worths = new List<int>();
        long margin = 0;
        sold = 0;
        allAtAsking = true;
        for (int day = 1; day <= 30; day++)
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
                    if (deal.Stage != OfferStage.Open)
                    {
                        continue;
                    }
                    if (deal.WantsDrive && !deal.Drove)
                    {
                        DealerRules.TestDrive(deal, car, day * 24 + h, s.Seed);
                        continue;
                    }
                    bool closes = DealerRules.StaffClose(y, deal, car);
                    if (!closes && !fixedPrices)
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
                        int price = DealerRules.Price(y, deal, car);
                        allAtAsking &= !fixedPrices || price == car.Asking;
                        margin += price - car.Cost;
                        sold++;
                        y.Deals.RemoveAt(d);
                        y.Cars.Remove(car);
                    }
                }
            }
        }
        return margin;
    }

    private static void DealerFixedSources()
    {
        string clock = EconomySource(@"Dealer\DealerDirector.Clock.cs"), actions = EconomySource(@"Dealer\DealerDirector.Actions.cs");
        string lots = EconomySource(@"Dealer\DealerApp.Lots.cs"), offers = EconomySource(@"Dealer\DealerApp.Offers.cs"), rules = EconomySource(@"Dealer\DealerRules.Customers.cs");
        Check(clock.Contains("DealerRules.StaffClose(y, deal, car) && Close(y, reg, deal, DealerRules.Price(y, deal, car), true)"),
            "dealer fixed: the staff close with the showroom's own rule and at its price (the asking price with fixed prices)");
        Check(MemberText(actions, "public bool Accept(").Contains("DealerRules.Price(y, d, car)") && MemberText(actions, "public bool Counter(").Contains("return Refuse(DealerRefusal.Fixed);"),
            "dealer fixed: taking an offer sells at the asking price there, and a counter is refused");
        string fixedPrices = MemberText(actions, "public void FixedPrices(");
        Check(fixedPrices.Contains("DealerRules.Withdraw(y)") && fixedPrices.Contains("Changed();"), "dealer fixed: the switch withdraws the offers below the asking price and saves");
        string close = MemberText(actions, "internal bool Close(");
        Check(close.Contains("y.FixedAtAsking += price == car.Asking ? 1 : 0;") && clock.Contains("FixedCheck(day);") && clock.Contains("Dealership check (fixed prices, '"),
            "dealer fixed: every sale with fixed prices is counted for the day's check line");
        Check(rules.Contains("FixedChance(OfferChance(car.Asking, worths[c], day - car.Bought, h.Skill, car.Condition, lift), y.Fixed)") && rules.Contains("AtAsking(offer, car);"),
            "dealer fixed: the hour draws fewer offers there with the showroom's own dice, each at the asking price");
        Check(lots.Contains("_director.FixedPrices(y.Place, !y.Fixed)") && lots.Contains("reallife_dealer_yard_fixed_on") && lots.Contains("if (!y.Fixed)"),
            "dealer fixed: the showroom's page shows the switch's state and its button, and no staff floor while it is on");
        Check(offers.Contains("Money(row, 3, DealerRules.Price(yard, deal, car));") && offers.Contains("int steps = yard.Fixed ? 1 : DealerRules.CounterSteps(d, car.Asking);")
              && offers.Contains("reallife_dealer_deal_fixed"),
            "dealer fixed: the Offers tab shows such offers at the asking price, with no counter");
    }

    private static void DealerFixedWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("dealer fixed", langs, codes))
        {
            return;
        }
        string[] keys =
        {
            "reallife_dealer_button_fixed_on", "reallife_dealer_button_fixed_off", "reallife_dealer_yard_fixed_on", "reallife_dealer_yard_fixed_off",
            "reallife_dealer_deal_fixed", "reallife_dealer_refuse_fixed"
        };
        var missing = new List<string>();
        bool plain = true;
        foreach (string key in keys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
            for (int l = 0; l < langs.Length; l++)
            {
                plain &= langs[l].TryGetValue(key, out string t) && !t.Contains("!") && !t.Contains("—") && !t.Contains("–") && t.Length <= 240;
            }
        }
        Check(missing.Count == 0, "dealer fixed: its texts are in every language with the same {names} (" + string.Join(", ", missing) + ")");
        Check(plain, "dealer fixed: its texts have no dash or exclamation mark and keep to 240 characters");
        bool guide = true;
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            guide &= langs[l].TryGetValue("reallife_guide_economy_dealer", out string g) && g.Contains("{dealer_fixed_offers}")
                     && langs[l].TryGetValue("reallife_dealer_button_fixed_on", out string b) && g.Contains("<b>" + b + "</b>");
        }
        Check(guide, "dealer fixed: the Guide's showroom page names the switch and the share of offers in English, Portuguese and Chinese");
        Check(EconomySource(@"Dealer\DealerGuide.cs").Contains(".Fact(\"dealer_fixed_offers\", () => Share(DealerCatalogue.FixedOffers))"),
            "dealer fixed: the Guide's share of offers comes from DealerCatalogue.FixedOffers");
        bool next = true;
        string[] says = { "fixed prices", "preços fixos", "固定价格" };
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            next &= WhatsNewHas(langs[l], says[l]);   // the next row, or the version row it is numbered into at release
        }
        Check(next, "dealer fixed: What's new has the line in English, Portuguese and Chinese");
    }
}
