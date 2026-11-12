using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;

// Selling a business with named buyers and rivals of the game's (Scripts/Economy/Services: SaleRules.Quality.cs,
// SaleRules.Buyers.cs, ServicesStore.cs): a business's quality in its value, what would raise the price, who bids and
// within what purse, the same buyer from the same save, no buyer twice, chains and investors, a rival's own sale (which
// business, at what price, who takes it after the player), the save's line with and without names, and the texts.
internal static partial class Program
{
    private static void ServicesBuyers()
    {
        SaleQuality();
        SaleRaises();
        SaleNamedOffers();
        SaleChainsInvestors();
        SaleRivalSales();
        SaleBuyersSave();
        SaleBuyersTexts();
    }

    private static SaleInputs SalePlain() => new SaleInputs { AvgProfit7 = 1000f, DaysOnRecord = 14, Sector = (int)Sector.Retail, Income = 100, Phase = 0, Fixtures = 20000, Deposit = 3000 };

    private static SaleInputs SaleKnown(int interior, int want, int rating, int skill, int staff)
    {
        SaleInputs i = SalePlain();
        i.Quality = true;
        i.Interior = interior;
        i.InteriorWant = want;
        i.Rating = rating;
        i.Skill = skill;
        i.Staff = staff;
        return i;
    }

    // ---------------------------------------------------------------- quality

    private static void SaleQuality()
    {
        Check(SaleRules.Value(SalePlain()).QualityPct == 100 && SaleRules.Value(SalePlain()).Goodwill == 60000, "sale quality: with nothing read of its quality a business is valued as before");
        SaleBreakdown middling = SaleRules.Value(SaleKnown(-1, 0, 50, 50, 4));
        Check(middling.QualityPct == 100 && middling.InteriorPts == 0 && middling.RatingPts == 0 && middling.SkillPts == 0 && middling.Goodwill == 60000,
            "sale quality: middling customers and staff and no interior to score count for nothing");

        SaleBreakdown shabby = SaleRules.Value(SaleKnown(40, 60, 50, 50, 4));
        Check(shabby.InteriorPts == -ServicesCatalogue.InteriorShortPct && shabby.QualityPct == 90 && shabby.Goodwill == 54000 && shabby.Fixtures == 20000,
            "sale quality: an interior under what the district's customers want takes ten points off the goodwill, not off the stock and furniture");
        SaleBreakdown atWant = SaleRules.Value(SaleKnown(60, 60, 50, 50, 4));
        SaleBreakdown full = SaleRules.Value(SaleKnown(100, 60, 50, 50, 4));
        SaleBreakdown half = SaleRules.Value(SaleKnown(80, 60, 50, 50, 4));
        Check(atWant.InteriorPts == 0 && full.InteriorPts == ServicesCatalogue.InteriorTopPct && half.InteriorPts == 2 && SaleRules.Value(SaleKnown(100, 100, 50, 50, 4)).InteriorPts == ServicesCatalogue.InteriorTopPct,
            "sale quality: at what the district wants the interior is level, a full score adds five in proportion above it (and a district that wants a full score is met by one)");

        SaleBreakdown loved = SaleRules.Value(SaleKnown(-1, 0, 90, 50, 4));
        SaleBreakdown hated = SaleRules.Value(SaleKnown(-1, 0, 10, 50, 4));
        Check(loved.RatingPts == 8 && hated.RatingPts == -8 && loved.Goodwill == 64800 && hated.Goodwill == 55200, "sale quality: customer satisfaction moves it a fifth of its distance from 50");
        SaleBreakdown skilled = SaleRules.Value(SaleKnown(-1, 0, 50, 80, 4));
        SaleBreakdown nobody = SaleRules.Value(SaleKnown(-1, 0, 50, 80, 0));
        Check(skilled.SkillPts == 3 && nobody.SkillPts == 0 && nobody.QualityPct == 100, "sale quality: the staff's skill counts a tenth of its distance from 50, and only with staff");

        SaleBreakdown worst = SaleRules.Value(SaleKnown(0, 60, 0, 0, 3));
        SaleBreakdown best = SaleRules.Value(SaleKnown(100, 0, 100, 100, 3));
        Check(worst.QualityPct == ServicesCatalogue.QualityLeastPct && best.QualityPct == ServicesCatalogue.QualityMostPct && worst.Value > worst.Fixtures,
            "sale quality: within its bounds, 85% at the worst and 120% at the best (" + worst.QualityPct + ", " + best.QualityPct + ")");
        SaleBreakdown odd = SaleRules.Value(SaleKnown(250, -40, 300, 900, 2));
        Check(odd.QualityPct <= ServicesCatalogue.QualityMostPct && odd.QualityPct >= ServicesCatalogue.QualityLeastPct && odd.InteriorPts == ServicesCatalogue.InteriorTopPct,
            "sale quality: figures out of range are held to 0 to 100");
        SaleInputs losing = SaleKnown(100, 0, 100, 100, 3);
        losing.AvgProfit7 = -300f;
        Check(SaleRules.Value(losing).Value == 20000, "sale quality: a business that loses money has no goodwill to raise, whatever its quality");
    }

    private static void SaleRaises()
    {
        var into = new List<SaleRaise>();
        SaleRules.Raises(SalePlain(), into);
        Check(into.Count == 0, "what would raise it: nothing is listed for a business with a full week of books and nothing read of its quality");

        SaleRules.Raises(SaleKnown(40, 60, 70, 60, 4), into);
        SaleRaise interior = into.Find(r => r.Kind == RaiseKind.Interior);
        SaleRaise rating = into.Find(r => r.Kind == RaiseKind.Rating);
        SaleRaise skill = into.Find(r => r.Kind == RaiseKind.Skill);
        Check(into.Count == 3 && into[0].Kind == RaiseKind.Interior && into[1].Kind == RaiseKind.Rating && into[2].Kind == RaiseKind.Skill, "what would raise it: the interior, customer satisfaction and staff skill, in that order");
        int now = SaleRules.Value(SaleKnown(40, 60, 70, 60, 4)).Value;
        Check(!interior.Met && interior.Now == 40 && interior.Want == 60 && interior.To == 60 && interior.Gain == SaleRules.Value(SaleKnown(60, 60, 70, 60, 4)).Value - now && interior.Gain == 6000,
            "what would raise it: the interior up to what the district wants, with what that adds worked out by valuing it again (" + interior.Gain + ")");
        Check(!rating.Met && rating.To == 80 && rating.Gain == SaleRules.Value(SaleKnown(40, 60, 80, 60, 4)).Value - now && rating.Gain > 0,
            "what would raise it: customer satisfaction a step of ten higher, and what that adds (" + rating.Gain + ")");
        Check(!skill.Met && skill.To == 70 && skill.Gain == SaleRules.Value(SaleKnown(40, 60, 70, 70, 4)).Value - now && skill.Gain > 0, "what would raise it: the staff's skill a step higher");

        SaleRules.Raises(SaleKnown(90, 60, 95, 100, 4), into);
        SaleRaise good = into.Find(r => r.Kind == RaiseKind.Interior);
        SaleRaise top = into.Find(r => r.Kind == RaiseKind.Skill);
        Check(good.Met && good.To == 100 && good.Gain > 0 && top.Met && top.To == 100 && top.Gain == 0 && into.Find(r => r.Kind == RaiseKind.Rating).Met,
            "what would raise it: lines already good are shown as good; one at the top adds nothing more");

        SaleInputs young = SaleKnown(60, 60, 50, 50, 2);
        young.DaysOnRecord = 3;
        SaleRules.Raises(young, into);
        SaleRaise books = into.Find(r => r.Kind == RaiseKind.Books);
        SaleInputs week = young;
        week.DaysOnRecord = ServicesCatalogue.HistoryDays;
        Check(books.Kind == RaiseKind.Books && books.Now == 3 && books.To == ServicesCatalogue.HistoryDays && books.Gain == SaleRules.Value(week).Value - SaleRules.Value(young).Value && books.Gain > 0,
            "what would raise it: a full week of books, for a business that has fewer days of them");

        SaleInputs losing = SaleKnown(40, 60, 30, 30, 2);
        losing.AvgProfit7 = -100f;
        SaleRules.Raises(losing, into);
        Check(into.Count == 0, "what would raise it: a business that loses money has nothing listed, since nothing raises a price it does not have");
    }

    // ---------------------------------------------------------------- named offers

    private static SaleBidder SaleRival(string id, string name, int purse, int count = 5) => new SaleBidder { Kind = (int)BuyerKind.Rival, Id = id, Name = name, Purse = purse, Count = count };

    private static void SaleNamedOffers()
    {
        Check(SaleRules.RivalPurse(1000f) == 26000 && SaleRules.RivalPurse(-50f) == 0 && SaleRules.RivalPurse(float.MaxValue) > 0, "named buyers: a rival can put up 26 weeks of its weekly takings");
        Check(SaleRules.RivalCanBid(68000, 80000) && !SaleRules.RivalCanBid(67990, 80000) && !SaleRules.RivalCanBid(1000000, 0), "named buyers: a rival bids only if it can put up 85% of the value");

        // Only a rival that can pay bids; it never offers more than it can put up.
        var s = new ServicesState { Seed = 777 };
        SaleListing l = SaleRules.List(s, "shop~1", "Corner Shop", 80000, 2, 100);
        var bidders = new List<SaleBidder> { SaleRival("rival_poor", "Poor Rival", 30000), SaleRival("rival_rich", "Rich Rival", 70000) };
        SaleOffer o = SaleRules.Offer(s, l, 101, bidders);
        Check(o != null && o.Buyer == (int)BuyerKind.Rival && o.Who == "rival_rich" && o.WhoName == "Rich Rival" && o.WhoCount == 5 && o.Named && o.Price <= 70000 && o.Ceiling <= 70000,
            "named buyers: the rival who can pay makes the offer by name, never above its purse (" + (o != null ? o.Price + "/" + o.Ceiling : "none") + ")");
        Check(SaleRules.Offer(s, l, 108, bidders) == null && s.SaleOffers.Count == 1, "named buyers: a buyer with an offer standing makes no second one, and with nobody else the turn brings none");

        // Whatever the turn's kind, the next kind with somebody makes it; with nobody at all, nothing.
        var s2 = new ServicesState { Seed = 778 };
        SaleListing l2 = SaleRules.List(s2, "shop~2", "Deli", 80000, 2, 100);
        var staffOnly = new List<SaleBidder> { new SaleBidder { Kind = (int)BuyerKind.Staff, Id = "emp_a", Name = "A. Clerk", Count = 6 } };
        SaleOffer so = SaleRules.Offer(s2, l2, 101, staffOnly);
        Check(so != null && so.Buyer == (int)BuyerKind.Staff && so.WhoName == "A. Clerk" && so.WhoCount == 6 && so.KeepsStaff && so.Parts == ServicesCatalogue.BuyerInstalments[(int)BuyerKind.Staff],
            "named buyers: with only the staff to buy, they make the offer on their own kind's terms");
        Check(SaleRules.Offer(s2, l2, 108, new List<SaleBidder>()) == null, "named buyers: with nobody at all, no offer comes");
        var unnamed = new ServicesState { Seed = 778 };
        SaleOffer old = SaleRules.Offer(unnamed, SaleRules.List(unnamed, "shop~2", "Deli", 80000, 2, 100), 101);
        Check(old != null && !old.Named && old.Who.Length == 0, "named buyers: the rule without buyers still makes an offer of a kind only");

        // The same save picks the same buyer, whatever order the game lists them in.
        var many = new List<SaleBidder>();
        for (int i = 0; i < 9; i++)
        {
            many.Add(SaleRival("rival_" + i, "Rival " + i, 900000));
        }
        string first = PickedName(many);
        many.Reverse();
        string again = PickedName(many);
        many.Sort((a, b) => string.CompareOrdinal(b.Name.Substring(6), a.Name.Substring(6)) * -1);
        Check(first != null && first == again && first == PickedName(many), "named buyers: the same save gives the same buyer, whatever order they come in (" + first + ")");
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var st = new ServicesState { Seed = seed };
            SaleOffer pick = SaleRules.Offer(st, SaleRules.List(st, "shop~9", "Cafe", 80000, 2, 100), 101, many);
            if (pick != null)
            {
                seen.Add(pick.Who);
            }
        }
        Check(seen.Count >= 5, "named buyers: across saves the rivals take turns (" + seen.Count + " of 9 seen in 40 saves)");
    }

    private static string PickedName(List<SaleBidder> bidders)
    {
        var s = new ServicesState { Seed = 4242 };
        SaleOffer o = SaleRules.Offer(s, SaleRules.List(s, "shop~5", "Cafe", 80000, 2, 100), 101, bidders);
        return o != null ? o.WhoName : null;
    }

    private static void SaleChainsInvestors()
    {
        var into = new List<SaleBidder>();
        bool listed = true;
        int asked = 0, found = 0;
        foreach (string type in new[] { "ba:businesstype_coffeeshop", "ba:businesstype_eventplanning", "ba:businesstype_florist", "ba:businesstype_cardealership", "ba:businesstype_nosuchtrade" })
        {
            for (int sector = 0; sector < (int)Sector.Count; sector++)
            {
                into.Clear();
                SaleRules.Chains(type, sector, into);
                asked++;
                found += into.Count > 0 ? 1 : 0;
                foreach (SaleBidder b in into)
                {
                    RealLifeOverhaul.Markets.CompanyDef d = RealLifeOverhaul.Markets.MarketCatalogue.Find(b.Count);
                    listed &= b.Kind == (int)BuyerKind.Chain && d != null && d.Name == b.Name && !d.Pipeline && !d.Holding;
                }
            }
        }
        bool tested = false;   // the trades and sectors above hold companies still to come and a tycoon's holding, so the rule is put to the test
        foreach (RealLifeOverhaul.Markets.CompanyDef d in RealLifeOverhaul.Markets.MarketCatalogue.All)
        {
            tested |= (d.Pipeline || d.Holding) && Array.IndexOf(ServicesCatalogue.ChainSectors, d.Sector) >= 0;
        }
        Check(listed && tested && found == asked, "chains: a chain buyer is one of the listed companies by its own name, never one still to come nor a tycoon's holding (" + found + " of " + asked + " found buyers)");
        into.Clear();
        SaleRules.Chains("ba:businesstype_nosuchtrade", (int)Sector.Office, into);
        bool bySector = into.Count > 0;
        foreach (SaleBidder b in into)
        {
            bySector &= RealLifeOverhaul.Markets.MarketCatalogue.Find(b.Count).Sector == ServicesCatalogue.ChainSectors[(int)Sector.Office];
        }
        Check(bySector, "chains: with no company in its trade, those of its sector buy");

        into.Clear();
        SaleRules.Investors(new[] { "An Investor", "", null, "Another" }, into);
        Check(into.Count == 2 && into[0].Name == "An Investor" && into[1].Id == "Another" && into[0].Kind == (int)BuyerKind.Investor, "investors: the save's investors by name, none empty");
        into.Clear();
        SaleRules.Investors(null, into);
        Check(into.Count == VenturesCatalogue.FirmNames.Length && into.Count > 0 && into[0].Name == VenturesCatalogue.FirmNames[0], "investors: with none in the save, the investment firms of the catalogue buy");
    }

    // ---------------------------------------------------------------- rivals selling their own

    private static RivalHolding SaleHolding(string place, string rival, int daily, int count) => new RivalHolding { Place = place, Business = "Shop " + place, Rival = rival, RivalName = "Name " + rival, Daily = daily, Count = count };

    private static void SaleRivalSales()
    {
        var s = new ServicesState { Seed = 99 };
        int due = 0, first = -1;
        for (int day = 0; day < 200; day++)
        {
            if (SaleRules.RivalSaleDue(s, day))
            {
                due++;
                first = first < 0 ? day : first;
            }
        }
        Check(due > 40 && due < 140 && first >= ServicesCatalogue.RivalSaleGapDays, "rivals selling: a sale may come on about four days in ten once the gap has passed (" + due + " of 200, first on " + first + ")");
        s.RivalSaleDay = 50;
        bool gap = true;
        for (int day = 51; day < 50 + ServicesCatalogue.RivalSaleGapDays; day++)
        {
            gap &= !SaleRules.RivalSaleDue(s, day);
        }
        Check(gap, "rivals selling: none comes within ten days of the last");

        var holdings = new List<RivalHolding>
        {
            SaleHolding("a~1", "r1", 900, 4), SaleHolding("b~2", "r2", 100, 1), SaleHolding("c~3", "r1", 200, 4), SaleHolding("d~4", "r3", 300, 2),
            SaleHolding("e~5", "r3", 50, 2), SaleHolding("f~6", "r4", 5000, 3), SaleHolding("g~7", "r4", 400, 3)
        };
        var picked = new HashSet<string>();
        for (int day = 0; day < 60; day++)
        {
            int at = SaleRules.PickHolding(s, holdings, day);
            picked.Add(at >= 0 ? holdings[at].Place : "none");
        }
        Check(picked.SetEquals(new[] { "e~5", "c~3", "d~4" }), "rivals selling: one of the city's three weakest is sold, never a rival's last business (" + string.Join(",", picked) + ")");
        holdings.Reverse();
        int a1 = SaleRules.PickHolding(s, holdings, 33);
        holdings.Reverse();
        int a2 = SaleRules.PickHolding(s, holdings, 33);
        Check(a1 >= 0 && holdings[holdings.Count - 1 - a1].Place == holdings[a2].Place, "rivals selling: the same day picks the same business, whatever order the rivals list them in");
        Check(SaleRules.PickHolding(s, new List<RivalHolding> { SaleHolding("z~1", "r9", 10, 1) }, 3) == -1 && SaleRules.PickHolding(s, new List<RivalHolding>(), 3) == -1,
            "rivals selling: with no rival that runs more than one, none is sold");

        Check(SaleRules.RivalAsking(100000f) == 95000 && SaleRules.RivalAsking(-5f) == 0 && SaleRules.RivalAsking(12345f) == ServicesCatalogue.Round10(11727), "rivals selling: the price is 95% of the game's own takeover price, a round figure");
        var buyers = new List<SaleBidder> { SaleRival("r1", "Seller", 999999), SaleRival("r2", "Small", 40000), SaleRival("r3", "Big", 200000), SaleRival("r4", "Bigger", 300000) };
        var chosen = new HashSet<string>();
        for (int day = 0; day < 40; day++)
        {
            int b = SaleRules.PickRivalBuyer(s, buyers, "r1", 95000, day);
            chosen.Add(b >= 0 ? buyers[b].Id : "none");
        }
        Check(chosen.SetEquals(new[] { "r3", "r4" }), "rivals selling: another rival who can pay takes it, never the seller (" + string.Join(",", chosen) + ")");
        Check(SaleRules.PickRivalBuyer(s, buyers, "r1", 5000000, 3) == -1 && SaleRules.PickRivalBuyer(s, buyers, "r1", 0, 3) == -1, "rivals selling: with nobody who can pay, nobody takes it");

        var open = new ServicesState { Seed = 5, NextId = 10 };
        RivalSale v = SaleRules.OpenRivalSale(open, SaleHolding("e~5", "r3", 50, 2), 95000, "r4", "Bigger", 70);
        Check(v != null && v.Id == 10 && open.NextId == 11 && open.RivalSaleDay == 70 && v.Deadline == 70 + ServicesCatalogue.RivalSaleOpenDays && v.Weekly == 350 && v.Buyer == "r4" && open.RivalSelling("e~5"),
            "rivals selling: a sale is opened for the player's days, with its takings a week");
        Check(SaleRules.OpenRivalSale(open, SaleHolding("e~5", "r3", 50, 2), 95000, null, null, 71) == null && SaleRules.OpenRivalSale(open, SaleHolding("q~1", "r3", 50, 2), 0, null, null, 71) == null,
            "rivals selling: never the same business twice, nor one with no price");
        RivalSale w = SaleRules.OpenRivalSale(open, SaleHolding("q~1", "r2", 60, 3), 50000, null, null, 72);
        Check(w != null && w.Buyer.Length == 0 && SaleRules.OpenRivalSale(open, SaleHolding("x~1", "r2", 60, 3), 50000, null, null, 73) == null && open.RivalSales.Count == ServicesCatalogue.RivalSalesMost,
            "rivals selling: two at most are open at once, and a sale with no rival to take it says so");
        Check(!SaleRules.RivalSaleOver(v, v.Deadline) && SaleRules.RivalSaleOver(v, v.Deadline + 1) && !SaleRules.RivalSaleDue(open, 200), "rivals selling: the player has every day to the last; with two open no new one comes");
    }

    private static void SaleBuyersSave()
    {
        var s = new ServicesState { Seed = 31337, NextId = 20, RivalSaleDay = 61 };
        s.Listings.Add(new SaleListing { Place = "Main%20St~12", Name = "Corner Shop", ListedDay = 42, Step = 3, Asking = 88000, Value = 80000, NextOfferDay = 45, OffersMade = 1 });
        s.SaleOffers.Add(new SaleOffer { Id = 8, Place = "Main%20St~12", Buyer = 0, Price = 66000, Ceiling = 70000, Parts = 1, ClauseDays = 90, Deadline = 47, Face = 1, Who = "rival|huang;^", WhoName = "Huang Guo", WhoCount = 12 });
        s.SaleOffers.Add(new SaleOffer { Id = 9, Place = "Main%20St~12", Buyer = 1, Price = 60000, Ceiling = 61000, Deadline = 48, Face = 2 });
        s.RivalSales.Add(new RivalSale { Id = 14, Place = "Side~3", Business = "Bean|Bar", Seller = "r1", SellerName = "One", Buyer = "r2", BuyerName = "Two", Price = 95000, Weekly = 700, Day = 60, Deadline = 65 });
        s.RivalSales.Add(new RivalSale { Id = 15, Place = "Low~9", Business = "Deli", Seller = "r3", SellerName = "Three", Price = 40000, Weekly = 300, Day = 61, Deadline = 66 });
        string line = ServicesStore.Write(s);
        ServicesState r = ServicesStore.Read(line, out int version);
        Check(r != null && version == ServicesStore.Version && version == 1 && ServicesStore.Write(r) == line, "named buyers' save: the same version, and what is read back writes the same line again");
        SaleOffer named = r.SaleOffer(8), plain = r.SaleOffer(9);
        Check(named.Named && named.Who == "rival huang  " && named.WhoName == "Huang Guo" && named.WhoCount == 12 && !plain.Named && plain.Who.Length == 0 && plain.Face == 2,
            "named buyers' save: an offer's buyer by name, with the dividers taken out, and one without a name");
        Check(r.RivalSaleDay == 61 && r.RivalSales.Count == 2 && r.RivalSale(14).Business == "Bean Bar" && r.RivalSale(14).BuyerName == "Two" && r.RivalSale(15).Buyer.Length == 0 && r.RivalSale(15).Deadline == 66,
            "named buyers' save: the rivals' sales and the day of the last");

        // A line written before named buyers: no names on its offers, no header field, no part of rivals' sales.
        var before = new ServicesState { Seed = 31337, NextId = 20 };
        before.Listings.Add(s.Listings[0]);
        before.SaleOffers.Add(new SaleOffer { Id = 9, Place = "Main%20St~12", Buyer = 3, Price = 60000, Ceiling = 61000, Deadline = 48, Face = 2 });
        string now = ServicesStore.Write(before);
        string old = now.Replace("|2|||0|0", "|2").Replace("^v:", string.Empty);
        int h = old.IndexOf("^h:", StringComparison.Ordinal), hEnd = old.IndexOf('^', h + 1);
        string header = old.Substring(h, hEnd - h);
        old = old.Substring(0, h) + header.Substring(0, header.LastIndexOf('|')) + old.Substring(hEnd);
        ServicesState back = ServicesStore.Read(old, out version);
        Check(old != now && !old.Contains("^v:") && back != null && back.SaleOffers.Count == 1 && !back.SaleOffers[0].Named && back.SaleOffers[0].Face == 2 && back.SaleOffers[0].Buyer == 3
              && back.RivalSales.Count == 0 && back.RivalSaleDay == 0 && ServicesStore.Write(back) == now,
            "named buyers' save: a line from before loads, its offers with no names and no rivals' sales");
        ServicesState junk = ServicesStore.Read(line + "^v:1|x;;7|p|b|s|n|||-5|-1|-2|-3", out version);
        Check(junk != null && junk.RivalSales.Count == 2, "named buyers' save: a part of rivals' sales holds two at most and skips a broken record");
    }

    private static readonly string[] SaleBuyerKeys =
    {
        "reallife_svc_test_rival_offer", "reallife_svc_test_rival_sale", "reallife_svc_test_no_rival", "reallife_svc_test_no_rival_sale", "reallife_svc_refuse_rival_gone",
        "reallife_svc_refuse_rival_sale_gone", "reallife_svc_note_rival_selling", "reallife_svc_note_rival_sold", "reallife_svc_confirm_buy_title", "reallife_svc_confirm_buy_body",
        "reallife_svc_who_staff", "reallife_svc_who_rival", "reallife_svc_who_chain", "reallife_svc_who_staff_note", "reallife_svc_who_investor", "reallife_svc_value_quality",
        "reallife_svc_raise_head", "reallife_svc_raise_none", "reallife_svc_raise_interior", "reallife_svc_raise_rating", "reallife_svc_raise_skill", "reallife_svc_raise_books",
        "reallife_svc_raise_met", "reallife_svc_raise_to", "reallife_svc_raise_to_skill", "reallife_svc_raise_days", "reallife_svc_kind_rival_sale", "reallife_svc_row_rival_sale",
        "reallife_svc_rsale_title", "reallife_svc_rsale_from", "reallife_svc_rsale_then", "reallife_svc_rsale_keeps", "reallife_svc_rsale_takings", "reallife_svc_rsale_how",
        "reallife_svc_rsale_buy"
    };

    private static void SaleBuyersTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("named buyers' texts", words, codes))
        {
            return;
        }
        foreach (string key in SaleBuyerKeys)
        {
            string lacking = LanguagesLacking(words, codes, key);
            Check(lacking.Length == 0, "named buyers' texts: " + key + " is in every language with the English names (" + lacking + ")");
        }
        foreach (string code in new[] { "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));   // its own file, not the English it falls back to
            string missing = string.Empty;
            foreach (string key in SaleBuyerKeys)
            {
                missing += own != null && own.TryGetValue(key, out string text) && text.Length > 0 ? string.Empty : key + " ";
            }
            Check(own != null && missing.Length == 0, "named buyers' texts: " + code + " has its own translation of every new text (" + missing.Trim() + ")");
        }
        string en(string key) => words[0].TryGetValue(key, out string v) ? v : string.Empty;
        bool fits = true;
        foreach (string notice in new[] { "reallife_svc_refuse_rival_gone", "reallife_svc_note_rival_selling", "reallife_svc_note_rival_sold", "reallife_svc_test_no_rival", "reallife_svc_test_no_rival_sale" })
        {
            fits &= en(notice).Length > 0 && en(notice).Length + 100 <= 240;   // names and an amount filled in
        }
        Check(fits, "named buyers' texts: each notice fits the game's notification with its names filled");
        string guide = en("reallife_guide_economy_sale");
        Check(guide.Contains("{sale_quality_least}") && guide.Contains("{sale_rival_weeks}") && guide.Contains("{sale_rsale_ask}") && guide.Contains("# Rivals selling")
              && guide.Contains("<b>" + en("reallife_svc_raise_head") + "</b>") && guide.Contains("<b>" + en("reallife_svc_rsale_buy") + "</b>")
              && EconomySource(@"Services\ServicesGuide.cs").Contains(".Fact(\"sale_rsale_ask\"") && EconomySource(@"Services\ServicesGuide.cs").Contains(".Fact(\"sale_quality_least\""),
            "named buyers' texts: the Guide's page says what quality counts, who buys and how rivals sell, its figures from the code");
        string mod = System.IO.File.ReadAllText(System.IO.Path.Combine(DesignScripts, "Core", "RealLifeOverhaulMod.cs"));
        Check(mod.Contains("AddButton(\"reallife_svc_test_rival_offer\", () => Economy.ServicesDirector.Instance?.TestRivalOffer())")
              && mod.Contains("AddButton(\"reallife_svc_test_rival_sale\", () => Economy.ServicesDirector.Instance?.TestRivalSale())"),
            "named buyers: the two tests are in Options > Tests");
        string[][] lines =
        {
            new[] { "en", "Selling a business: every offer comes from a buyer by name." }, new[] { "pt", "Venda de uma empresa: toda oferta vem de um comprador com nome." },
            new[] { "zh-cn", "出售企业：每份报价都来自一位有名字的买家。" }, new[] { "de", "Verkauf eines Unternehmens: Jedes Angebot kommt von einem Käufer mit Namen." },
            new[] { "fr", "Vendre une entreprise : chaque offre vient d'un acheteur nommé." }, new[] { "es", "Vender un negocio: cada oferta viene de un comprador con nombre." },
            new[] { "ru", "Продажа бизнеса: каждое предложение приходит от покупателя с именем." }, new[] { "nl", "Bedrijf verkopen: elk bod komt van een koper met een naam." },
            new[] { "tr", "İşletme satmak: her teklif adı olan bir alıcıdan gelir." }, new[] { "pl", "Sprzedaż firmy: każda oferta pochodzi od kupującego z nazwiskiem." },
            new[] { "ko", "사업체 매각: 모든 제안은 이름이 있는 구매자에게서 옵니다." }
        };
        foreach (string[] row in lines)
        {
            int l = Array.IndexOf(codes, row[0]);   // Polish and Korean are not per-feature languages: their own files are read
            Dictionary<string, string> own = l >= 0 ? words[l] : LoadWords(System.IO.Path.Combine(LocalesFolder(), row[0] + ".json"));
            Check(own != null && WhatsNewHas(own, row[1]), "named buyers' texts: What's new has the feature in " + row[0]);
        }
    }
}
