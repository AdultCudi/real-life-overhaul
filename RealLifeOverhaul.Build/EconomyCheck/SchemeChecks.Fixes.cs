using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Schemes;
using RealLifeOverhaul.Tech;
using SchemeOffice = RealLifeOverhaul.Schemes.Office;

// Schemes and inquiries: what a logic review found, one group of checks for each fault. Every check drives a rule the shipped director (or the
// company's, the market's, the investors') calls, with the facts that made the fault; each was made to fail once by breaking its rule.
internal static partial class Program
{
    private static void SchemeFixChecks()
    {
        SchemeBooksStampChecks();
        SchemeCoopChecks();
        SchemeWatchedChecks();
        SchemeNoCourtChecks();
        SchemeFindingsAreNotMisuseChecks();
        SchemeCatchUpChecks();
        SchemeQuietChecks();
        SchemeFundReturnChecks();
        SchemeInventedClaimChecks();
        SchemeWashDoesNotRepayClubChecks();
        SchemeSeedChecks();
        SchemeRefusalChecks();
        SchemeDispatchChecks();
    }

    // ------------------------------------------------------------------------------------------------- 1: the lift is read when the price is made

    private static void SchemeBooksStampChecks()
    {
        // Stamped on the listing when it is valued and on the offer when its price is made.
        var sv = new ServicesState { Seed = 777 };
        SaleListing l = SaleRules.List(sv, "shop~1", "Corner Shop", 80000, 2, 100, SchemeCatalogue.LiftMost);
        var bidders = new List<SaleBidder> { new SaleBidder { Kind = (int)BuyerKind.Investor, Id = "inv", Name = "Inv", Purse = 0, Count = 0 } };
        SaleOffer offer = null;
        for (int day = 101; day <= 140 && offer == null; day++)
        {
            offer = SaleRules.OfferDue(l, day) ? SaleRules.Offer(sv, l, day, bidders) : null;
        }
        l.Lift = 0;   // the listing is valued again on plain books: the offer already made keeps the books its price was made from
        Check(offer != null && offer.Lift == SchemeCatalogue.LiftMost && SaleRules.List(sv, "shop~1", "Corner Shop", 80000, 2, 100).Lift == 0,
            "schemes books: an offer carries the lift of the books its price was made from, and a listing valued on plain books carries none");

        string line = ServicesStore.Write(sv);
        ServicesState back = ServicesStore.Read(line, out _);
        sv.Listings[0].Lift = 100;
        ServicesState listed = ServicesStore.Read(ServicesStore.Write(sv), out _);
        ServicesState old = ServicesStore.Read(SchemeWithoutLastColumn(SchemeWithoutLastColumn(ServicesStore.Write(sv), "l"), "f"), out _);
        Check(back != null && back.SaleOffers.Count == 1 && back.SaleOffers[0].Lift == SchemeCatalogue.LiftMost && listed.Listings[0].Lift == 100
              && old != null && old.SaleOffers.Count == 1 && old.SaleOffers[0].Lift == 0 && old.Listings[0].Lift == 0 && old.SaleOffers[0].Price == offer.Price,
            "schemes books: the stamp is saved with the listing and the offer, and a save from before it loads with plain books on both");

        // The exploit: list with a 30% lift, take the offer's price, set the lift back to 0, accept. The buyer looks by the stamp.
        int seed = SchemeSeedWhere(k => SchemeRules.Line(k, SchemeKind.Books, 100, 0) > 600);
        SchemeWorld w = SchemeWorldOf();
        int looked = 0, walked = 0, plainLooked = 0;
        for (int id = 1; id <= 60; id++)
        {
            SchemeState s = SchemeStateOf(seed, 100, w);
            SchemeShop shop = s.Shop(SchemePlace, true);
            shop.Lift = 0;   // the player set the lift back before accepting
            int heat = SchemeHeat(s, SchemeKind.Books);
            bool seen = SchemeRules.Looked(s, SchemePlace, (int)BuyerKind.Investor, offer.Lift, id);
            looked += seen ? 1 : 0;
            walked += seen && SchemeHeat(s, SchemeKind.Books) - heat == SchemeCatalogue.HeatWalked ? 1 : 0;
            plainLooked += SchemeRules.Looked(s, SchemePlace, (int)BuyerKind.Investor, 0, id) ? 1 : 0;
        }
        Check(looked >= 36 && looked <= 60 && walked == looked && plainLooked == 0,
            "schemes books: a buyer looks at the books by the lift stamped on the offer, though the lift was set back to nothing before accepting: " + looked + " of 60 offers seen through, plain books never");

        // The sale: the act is the part of the price the stamped lift made, whatever the books show at the end; a price made on plain books is no act.
        SchemeState sale = SchemeStateOf(seed, 100, w);
        sale.Shop(SchemePlace, true).Lift = 0;
        SchemeAct dressed = SchemeRules.Sold(sale, SchemePlace, 39000, SchemeCatalogue.LiftMost, 101);
        SchemeState late = SchemeStateOf(seed, 100, w);
        late.Shop(SchemePlace, true).Lift = SchemeCatalogue.LiftMost;   // lifted only after the price was made
        SchemeAct none = SchemeRules.Sold(late, SchemePlace, 39000, 0, 101);
        Check(dressed != null && dressed.Gain == SchemeRules.Over(39000, SchemeCatalogue.LiftMost) && dressed.Gain == 9000 && SchemeRules.Count(sale, SchemeKind.Books, 101) == 1
              && none == null && SchemeRules.Count(late, SchemeKind.Books, 101) == 0 && late.Shop(SchemePlace, false).Lift == 0,
            "schemes books: a business sold on a price made from lifted books puts the lift's part on the trail though the lift was taken off, and a price made on plain books puts none");

        // The quick sale's buyer looks too, by an id of its own that no offer has, the same quick sale always the same way.
        int p1 = SaleRules.QuickLookId(70000), p2 = SaleRules.QuickLookId(70010);
        int seenQuick = 0;
        bool stable = true;
        for (int price = 60000; price < 60300; price++)
        {
            SchemeState q = SchemeStateOf(seed, 100, w);
            bool seen = SchemeRules.Looked(q, SchemePlace, (int)BuyerKind.Investor, SchemeCatalogue.LiftMost, SaleRules.QuickLookId(price));
            seenQuick += seen ? 1 : 0;
            stable &= seen == SchemeRules.SeenThrough(seed, SchemePlace, (int)BuyerKind.Investor, SchemeCatalogue.LiftMost, SaleRules.QuickLookId(price));
        }
        Check(p1 < 0 && p2 < 0 && p1 != p2 && p1 == SaleRules.QuickLookId(70000) && stable && seenQuick > 300 * 650 / 1000 && seenQuick < 300 * 950 / 1000 && SaleRules.QuickLookId(0) < 0,
            "schemes books: a quick sale's buyer looks at the books like any other (" + seenQuick + " of 300 prices seen through at the full lift), by an id no offer has");

        // Every kind of buyer looks, a rival who takes over the player's business included.
        var byKind = new int[(int)BuyerKind.Count];
        for (int id = 1; id <= 1000; id++)
        {
            for (int kind = 0; kind < byKind.Length; kind++)
            {
                byKind[kind] += SchemeRules.SeenThrough(seed, SchemePlace, kind, SchemeCatalogue.LiftMost, id) ? 1 : 0;
            }
        }
        bool eachLooks = SchemeCatalogue.Diligence.Length == (int)BuyerKind.Count;
        for (int kind = 0; kind < byKind.Length; kind++)
        {
            eachLooks &= Math.Abs(byKind[kind] - SchemeCatalogue.Diligence[kind]) <= 60;
        }
        Check(eachLooks && byKind[(int)BuyerKind.Rival] > 350 && byKind[(int)BuyerKind.Rival] < 550,
            "schemes books: every kind of buyer looks as hard as its kind does at the full lift, a rival taking the business over " + byKind[(int)BuyerKind.Rival] / 10.0 + "% of the time");
    }

    // A line of the services' save with the last column of each record of a section taken off, as a save from before that column wrote it.
    private static string SchemeWithoutLastColumn(string line, string tag)
    {
        string head = "^" + tag + ":";
        int a = line.IndexOf(head, StringComparison.Ordinal), b = line.IndexOf('^', a + 1);
        string[] records = line.Substring(a + head.Length, b - a - head.Length).Split(';');
        for (int i = 0; i < records.Length; i++)
        {
            records[i] = records[i].Substring(0, records[i].LastIndexOf('|'));
        }
        return line.Substring(0, a) + head + string.Join(";", records) + line.Substring(b);
    }

    // ------------------------------------------------------------------------------------------------- 2: complied is the inquiry's own cooperation

    private static void SchemeCoopChecks()
    {
        var plain = new SchemeState { Seed = 5 };
        var asked = new List<bool>();
        Func<string, int, long, int, bool, int> court = (charge, evidence, amount, seed, coop) =>
        {
            asked.Add(coop);
            return asked.Count;
        };
        var full = new SchemeInquiry { Id = 3, Kind = SchemeKind.Expense, Evidence = SchemeCatalogue.EvidenceMost, Coop = false };
        var told = new SchemeInquiry { Id = 4, Kind = SchemeKind.Expense, Evidence = SchemeCatalogue.CoopEvidence, Coop = true };
        var thin = new SchemeInquiry { Id = 5, Kind = SchemeKind.Expense, Evidence = SchemeCatalogue.ChargeFrom, Coop = true };
        int a = SchemeRules.FileCase(plain, full, JusticeLaw.TaxEvasion, 9000, court);
        int b = SchemeRules.FileCase(plain, told, JusticeLaw.TaxEvasion, 9000, court);
        int c = SchemeRules.FileCase(plain, thin, JusticeLaw.TaxEvasion, 9000, court);
        Check(a == 1 && b == 2 && c == 3 && !asked[0] && asked[1] && asked[2] && SchemeCatalogue.EvidenceMost >= SchemeCatalogue.CoopEvidence
              && SchemeRules.FileCase(plain, full, JusticeLaw.TaxEvasion, 9000, null) == 0,
            "schemes coop: a file at the most the evidence can be is not 'complied' unless the player told the investigators everything, and one who did is, whatever the evidence");
    }

    // ------------------------------------------------------------------------------------------------- 3: a club file paid back while the cash is watched

    private static void SchemeWatchedChecks()
    {
        int seed = SchemeSeedWhere(k => SchemeRules.Line(k, SchemeKind.Club, 10, 0) > 600);
        SchemeWorld w = SchemeWorldOf();
        foreach (bool watched in new[] { false, true })
        {
            SchemeState s = SchemeStateOf(seed, 10, w);
            SchemeRules.ClubSold(s, SchemePlace, 9000, 50, SchemeCatalogue.Small, 10);
            s.Watched = watched;
            long cost = SchemeRules.RepayCost(s, SchemeKind.Club, 11, null), cash = SchemeRules.RepayCash(s, SchemeKind.Club, cost), inHand = s.Dirty;
            long paid = SchemeRules.Repay(s, SchemeKind.Club, 11, null, out long fromCash);
            SchemeRules.Due due = SchemeRules.RepayDue(SchemeKind.Club, paid, fromCash);
            Check(cost == 9000 && paid == 9000 && fromCash == cash && cash == 9000 && s.Dirty == inHand - paid && due.Wallet == 0 && due.Households == 9000,
                "schemes watched: paying a club file back takes the whole of it out of the cash in hand when that covers it, " + (watched ? "watched" : "not watched")
                + ": the cash in hand falls by what was repaid and the wallet pays nothing");
        }
        SchemeState partial = SchemeStateOf(seed, 10, w);
        SchemeRules.ClubSold(partial, SchemePlace, 9000, 50, SchemeCatalogue.Small, 10);
        partial.Dirty = 2500;
        partial.Watched = true;
        long paidPart = SchemeRules.Repay(partial, SchemeKind.Club, 11, null, out long fromHand);
        SchemeRules.Due remainder = SchemeRules.RepayDue(SchemeKind.Club, paidPart, fromHand);
        Check(paidPart == 9000 && fromHand == 2500 && partial.Dirty == 0 && remainder.Wallet == 6500 && remainder.Households == 9000
              && SchemeRules.RepayCash(partial, SchemeKind.Claim, 5000) == 0 && SchemeRules.RepayCash(partial, SchemeKind.Club, 100) == 0,
            "schemes watched: with less cash in hand than the file, the cash in hand all goes (watched or not) and the wallet pays the rest; only a club's file is paid from the cash in hand");
    }

    // ------------------------------------------------------------------------------------------------- 4: justice switched off

    private static void SchemeNoCourtChecks()
    {
        int seed = SchemeSeedWhere(k => !SchemeRules.Spotted(k, SchemeKind.Club, 0) && SchemeRules.Line(k, SchemeKind.Club, 10, 0) > 600);
        SchemeWorld w = SchemeWorldOf();
        SchemeState s = SchemeStateOf(seed, 10, w);
        SchemeRules.ClubSold(s, SchemePlace, 9000, 50, SchemeCatalogue.Large, 10);
        var events = new List<SchemeEvent>();
        SchemeInquiry q = SchemeRules.OpenNow(s, SchemeKind.Club, 10, events);
        int day = 10;
        SchemeUntil(s, ref day, 80, w, SchemeEventKind.Findings, out _);
        q.Case = 7;   // the court took the case; then justice was switched off
        bool wasWatched = s.Watched;
        Check(q.Stage == InquiryStage.Charged && q.Open && wasWatched && SchemeRules.Verdict(q, true, 0) == 0,
            "schemes no court: an inquiry charged at the court's door waits while the court runs, and its cash stays watched");
        int none = SchemeRules.Verdict(q, false, 0);
        SchemeRules.Close(s, q, (InquiryEnd)none, day + 1);
        Check(none == (int)InquiryEnd.Settled && q.End == InquiryEnd.Settled && !q.Open && !s.Watched && s.OpenOf(SchemeKind.Club) == null,
            "schemes no court: with the courts switched off the inquiry closes as settled and the watch on the cash in hand ends, where it would wait for a case nobody runs");
        var other = new SchemeInquiry { Id = 9, Kind = SchemeKind.Expense, Stage = InquiryStage.Charged, Case = 0 };
        var quiet = new SchemeInquiry { Id = 10, Kind = SchemeKind.Expense, Stage = InquiryStage.Letter, Case = 0 };
        Check(SchemeRules.Verdict(other, true, 0) == (int)InquiryEnd.Settled && SchemeRules.Verdict(quiet, false, 0) == 0 && SchemeRules.Verdict(null, false, 0) == 0
              && SchemeRules.Verdict(new SchemeInquiry { Stage = InquiryStage.Charged, Case = 4 }, true, (int)InquiryEnd.Convicted) == (int)InquiryEnd.Convicted
              && SchemeRules.Verdict(new SchemeInquiry { Stage = InquiryStage.Charged, Case = 4 }, true, 99) == (int)InquiryEnd.Settled
              && SchemeRules.Verdict(new SchemeInquiry { Stage = InquiryStage.Charged, Case = 4 }, true, (int)InquiryEnd.Cleared) == (int)InquiryEnd.Cleared,
            "schemes no court: a charged file with no case opened ends as settled, one not yet charged is never ended by it, and the court's answer, when there is a court, is the inquiry's");
    }

    // ------------------------------------------------------------------------------------------------- 5: what an inquiry takes is not also misuse of a firm's money

    private static void SchemeFindingsAreNotMisuseChecks()
    {
        foreach (string key in new[] { SchemeCatalogue.FoundKey, SchemeCatalogue.SurchargeKey, SchemeCatalogue.RepayKey })
        {
            Cap own = FundedCap();
            long counted = StartupMoney.Shortfall(new List<Cap> { own }, 0, Raise, 12, false, key);
            Check(SchemeCatalogue.IsFinding(key) && counted == 0 && own.Acc.Taken == 0 && own.Acc.Misuse.Count == 0,
                "schemes investors: what an inquiry takes by force (" + key + ") under the investors' floor counts as no misuse of a firm's money");
        }
        Cap spent = FundedCap();
        long other = StartupMoney.Shortfall(new List<Cap> { spent }, 0, Raise, 12, false, "ba:transaction_boatpurchase");
        Cap favour = FundedCap();
        long paid = StartupMoney.Shortfall(new List<Cap> { favour }, 0, Raise, 12, false, "reallife_transaction_scheme_favour");
        Check(other == Raise && spent.Acc.Taken == Raise && paid == Raise && !SchemeCatalogue.IsFinding("reallife_transaction_scheme_favour") && !SchemeCatalogue.IsFinding(null)
              && !SchemeCatalogue.IsFinding(string.Empty) && !SchemeCatalogue.IsFinding("reallife_transaction_scheme_wash"),
            "schemes investors: the same drop under the floor from anything else still counts as the investors' money spent elsewhere, the player's own payments included");
    }

    // ------------------------------------------------------------------------------------------------- 6: a long gap is played day by day

    private static void SchemeCatchUpChecks()
    {
        int seed = SchemeSeedWhere(k => !SchemeRules.Spotted(k, SchemeKind.Expense, 0) && !SchemeRules.Spotted(k, SchemeKind.Expense, 1) && SchemeRules.Line(k, SchemeKind.Expense, 10, 0) > 500);
        SchemeWorld w = SchemeWorldOf();
        Func<SchemeState> make = () =>
        {
            SchemeState s = SchemeStateOf(seed, 10, w);
            SchemeRules.Expensed(s, 10, 3000, SchemeCatalogue.Small);
            SchemeRules.Expensed(s, 12, 3000, SchemeCatalogue.Small);
            s.Heat[(int)SchemeKind.Expense] = SchemeCatalogue.HeatMost;
            return s;
        };
        SchemeState byDay = make(), jumped = make();
        var daily = new List<SchemeEvent>();
        var gap = new List<SchemeEvent>();
        var one = new List<SchemeEvent>();
        for (int d = 11; d <= 90; d++)
        {
            one.Clear();
            SchemeRules.Day(byDay, d, w, one);
            daily.AddRange(one);
        }
        SchemeRules.Day(jumped, 90, w, gap);   // nobody played the 80 days between
        bool sameEvents = daily.Count == gap.Count;
        for (int i = 0; sameEvents && i < daily.Count; i++)
        {
            sameEvents &= daily[i].Kind == gap[i].Kind && daily[i].Amount == gap[i].Amount && daily[i].Inquiry == gap[i].Inquiry;
        }
        SchemeInquiry q = jumped.Cases.Count > 0 ? jumped.Cases[0] : null;
        Check(SchemeStore.Write(byDay) == SchemeStore.Write(jumped) && sameEvents && q != null && q.End == InquiryEnd.Charged && q.Evidence >= SchemeCatalogue.ChargeFrom
              && gap.Exists(e => e.Kind == SchemeEventKind.Findings) && !gap.Exists(e => e.Kind == SchemeEventKind.Dropped),
            "schemes catch-up: 80 days nobody played end as the same days played one by one: the same state, the same events, and the inquiry that would have charged is charged, never dropped for the gap");

        // A day still costs a few loops: a game's worth of days with a busy file is milliseconds.
        SchemeState busy = SchemeStateOf(seed, 10, w);
        for (int i = 0; i < 20; i++)
        {
            SchemeRules.Claimed(busy, SchemePlace, 10 + i, 2000, SchemeCatalogue.Small, i + 1);
            SchemeRules.ClubSold(busy, SchemePlace, 3000, 50, SchemeCatalogue.Small, 10 + i);
        }
        var sink = new List<SchemeEvent>();
        var watch = Stopwatch.StartNew();
        SchemeRules.Day(busy, 10 + 3650, w, sink);
        watch.Stop();
        Check(busy.Day == 10 + 3650 && watch.ElapsedMilliseconds < 1500,
            "schemes catch-up: ten game years nobody played are played day by day in " + watch.ElapsedMilliseconds + " ms (" + sink.Count + " events), so the replay is bounded in practice");
    }

    // ------------------------------------------------------------------------------------------------- 7 and 8: what rests, rests

    private static void SchemeQuietChecks()
    {
        int seed = SchemeRules.SeedOf("quiet");
        foreach (bool on in new[] { true, false })
        {
            SchemeWorld w = SchemeWorldOf(on);
            SchemeState s = SchemeStateOf(seed, 10, w);
            s.Shop(SchemePlace, true).Lift = SchemeCatalogue.LiftMost;
            var events = new List<SchemeEvent>();
            for (int d = 11; d <= 20; d++)
            {
                SchemeRules.Day(s, d, w, events);
            }
            Check(on ? SchemeHeat(s, SchemeKind.Books) > 0 : SchemeHeat(s, SchemeKind.Books) == 0 && s.Shop(SchemePlace, false).Lift == SchemeCatalogue.LiftMost,
                "schemes books: dressed books " + (on ? "heat the trail by the day while the option is on" : "heat nothing while the option is off, and are not forgotten"));
        }
        SchemeWorld calm = SchemeWorldOf();
        SchemeWorld session = SchemeWorldOf();
        session.Session = true;
        foreach (bool inSession in new[] { false, true })
        {
            SchemeState s = SchemeStateOf(seed, 10, inSession ? session : calm);
            for (int i = 0; i < s.Rival.Length; i++)
            {
                s.Rival[i].Heat = 300;
            }
            var events = new List<SchemeEvent>();
            for (int d = 11; d <= 30; d++)
            {
                SchemeRules.Day(s, d, inSession ? session : calm, events);
            }
            Check(inSession ? s.Rival[0].Heat == 300 && s.Rival[1].Heat == 300 && s.Rival[2].Heat == 300 : s.Rival[0].Heat < 300,
                "schemes rivals: in a Going Public session a rival's heat does not move (" + s.Rival[0].Heat + " after 20 days), out of one it fades");
        }
    }

    // ------------------------------------------------------------------------------------------------- 9: the honest fund is paid out of the rival's worth

    private static void SchemeFundReturnChecks()
    {
        bool never = true;
        long worstLook = long.MinValue;
        for (long amount = SchemeCatalogue.FundLeast; amount <= SchemeCatalogue.FundMost; amount += 100)
        {
            long gain = SchemeRules.FundBack(amount) - amount, fee = SchemeRules.FundFee(amount, 1000);
            never &= gain <= fee;
            worstLook = Math.Max(worstLook, gain - fee);
        }
        Check(never && worstLook <= 0,
            "schemes fund: for every sum a fund can ask, the honest return is no more than the fee of having it looked at, so looking first and then investing nets nothing or less (worst case " + worstLook + ")");
        long worth = 20000;
        Check(SchemeRules.FundPaidOut(12000, worth) == SchemeRules.FundBack(12000) && SchemeRules.FundPaidOut(12000, 12100) == 12100 && SchemeRules.WalletOf(new SchemeEvent { Kind = SchemeEventKind.FundPaid, Net = 12100 }) == 12100
              && SchemeRules.RivalMove(new SchemeEvent { Kind = SchemeEventKind.FundPaid, Net = 12100 }) == -12100,
            "schemes fund: what a fund pays into the wallet is exactly what leaves the rival's worth");

        // A rival who is gone has no worth to pay out of: the game's worth moves only for an active one, so the fund pays nothing rather than money from nowhere.
        int honestSeed = SchemeSeedWhere(k => { var c = SchemeCityWorld(); var x = SchemeStateOf(k, 500, c); SchemeRules.FundNow(x, 500, c, new List<SchemeEvent>()); return !x.Fund.Sham; });
        SchemeWorld city = SchemeCityWorld();
        SchemeState held = SchemeStateOf(honestSeed, 500, city);
        SchemeRules.FundNow(held, 500, city, new List<SchemeEvent>());
        SchemeRules.FundPlace(held, 500);
        city.RivalActive[held.Fund.Tycoon] = false;
        var paid = new List<SchemeEvent>();
        for (int d = 501; d <= 530; d++)
        {
            SchemeRules.Day(held, d, city, paid);
        }
        SchemeEvent gone = paid.Find(e => e.Kind == SchemeEventKind.FundPaid);
        Check(gone.Kind == SchemeEventKind.FundPaid && gone.Net == 0 && SchemeRules.WalletOf(gone) == 0 && SchemeRules.RivalMove(gone) == 0,
            "schemes fund: a fund whose rival has gone pays nothing, so the wallet never gets what no worth gave up");
    }

    // ------------------------------------------------------------------------------------------------- 10: an invented claim is never a real break-in's

    private static void SchemeInventedClaimChecks()
    {
        var company = new CompanyState();
        var policy = new Policy();
        CoverRules.Buy(policy, 1, 20, 1, 60);
        int theft = (int)CoverKind.Theft;
        Claim real = CoverRules.Open(company, policy, SchemePlace, "Shop", theft, 6000, 10);
        Claim invented = CoverRules.OpenInvented(company, policy, "Oak~1", "Other", theft, 6000, 12);
        int payoutBefore = invented.Payout;
        Claim newest = CoverRules.NewestTheft(company.Claims, 13, 7);
        bool onlyInvented = CoverRules.NewestTheft(new List<Claim> { invented }, 13, 7) == null;
        bool refused = !CoverRules.Reported(invented);
        bool realReported = CoverRules.Reported(newest);
        CompanyState back = CompanyStore.Read(CompanyStore.Write(company), out _);
        Check(invented.Id > real.Id && invented.Invented && !real.Invented && newest == real && onlyInvented && refused && invented.Payout == payoutBefore && invented.Excess > 0 && realReported
              && real.Payout == 6000 && back.ClaimOf(invented.Id).Invented && !back.ClaimOf(real.Id).Invented && invented.Stage == ClaimStage.Filed,
            "schemes claim: a police report of a real break-in is the real theft claim's though an invented one is newer, never an invented claim's, and its payout stays what the trail recorded");
    }

    // ------------------------------------------------------------------------------------------------- 11: a wash finding touches only the washing

    private static void SchemeWashDoesNotRepayClubChecks()
    {
        int seed = SchemeSeedWhere(k => !SchemeRules.Spotted(k, SchemeKind.Club, 0) && SchemeRules.Line(k, SchemeKind.Club, 10, 0) > 600 && SchemeRules.Line(k, SchemeKind.Wash, 10, 0) > 600);
        SchemeWorld w = SchemeWorldOf();
        SchemeState s = SchemeStateOf(seed, 10, w);
        SchemeRules.ClubSold(s, SchemePlace, 10000, 50, SchemeCatalogue.Small, 10);
        s.Shop(SchemePlace, true).Wash = SchemeCatalogue.WashSteps[2];
        var events = new List<SchemeEvent>();
        for (int d = 11; d <= 20; d++)
        {
            SchemeRules.Day(s, d, w, events);
        }
        var clubQ = new SchemeInquiry { Id = 99, Kind = SchemeKind.Club, Opened = 20 };   // what a club inquiry would hold, asked of the file
        int holdsBefore = SchemeRules.Holds(s, clubQ, 20);
        SchemeAct club = s.Acts.Find(a => a.Kind == SchemeKind.Club);
        SchemeRules.OpenNow(s, SchemeKind.Wash, 20, events);
        int day = 20;
        SchemeUntil(s, ref day, 90, w, SchemeEventKind.Findings, out SchemeEvent washFound);
        Check(washFound.A == (int)SchemeKind.Wash && washFound.Amount > 0 && club.Back == 0 && club.Owed == 10000 && SchemeRules.Holds(s, clubQ, 20) == holdsBefore
              && SchemeRules.Owed(s, SchemeKind.Club, day) == 10000 && s.ClubBack == washFound.Amount,
            "schemes wash: a till's finding gives the households their money back and touches no club act: the club's file still owes what it did and holds as much against the player");
        long ledger = SchemeRules.ClubOwed(s), cost = SchemeRules.RepayCost(s, SchemeKind.Club, day, null);
        long paid = SchemeRules.Repay(s, SchemeKind.Club, day, null, out _);
        Check(ledger == 10000 - washFound.Amount && cost == ledger && paid == ledger && s.ClubBack == s.ClubTaken && SchemeRules.Recover(s, 1) == 0,
            "schemes wash: paying the club back after the till's finding costs only what the households are still out, so no unit is returned to them twice (" + paid + " of 10000)");
    }

    // ------------------------------------------------------------------------------------------------- 12: a second save on the same city

    private static void SchemeSeedChecks()
    {
        string city = "city-seed-1234";
        int first = SchemeRules.SeedOf(SchemeRules.PlaythroughOf(city, "character-AAAA")), second = SchemeRules.SeedOf(SchemeRules.PlaythroughOf(city, "character-BBBB"));
        int again = SchemeRules.SeedOf(SchemeRules.PlaythroughOf(city, "character-AAAA"));
        var answers = new int[2];
        int differs = 0, tempers = 40;
        for (int i = 0; i < tempers; i++)
        {
            string person = SchemeRules.Contact(i);
            differs += SchemeRules.TemperOf(first, SchemeOffice.Contact, person) != SchemeRules.TemperOf(second, SchemeOffice.Contact, person) ? 1 : 0;
            answers[0] += SchemeRules.TemperOf(first, SchemeOffice.Contact, person) == SchemeRules.TemperOf(again, SchemeOffice.Contact, person) ? 1 : 0;
        }
        bool lines = false;
        for (int k = 0; k < SchemeCatalogue.Kinds; k++)
        {
            lines |= SchemeRules.Line(first, (SchemeKind)k, 100, 0) != SchemeRules.Line(second, (SchemeKind)k, 100, 0);
        }
        Check(first != second && first == again && differs >= tempers / 4 && answers[0] == tempers && lines && SchemeRules.PlaythroughOf(city, "character-AAAA") != SchemeRules.PlaythroughOf(city, "character-BBBB")
              && SchemeRules.PlaythroughOf(city, null) == city && SchemeRules.PlaythroughOf(null, "character-AAAA") == "character-AAAA" && SchemeRules.PlaythroughOf("", string.Empty) == "city"
              && first != 0,
            "schemes seed: two playthroughs on the same city draw other tempers and other lines (" + differs + " of " + tempers + " people answer differently), the same save always the same");
    }

    // ------------------------------------------------------------------------------------------------- refusals before any money moves

    private static void SchemeRefusalChecks()
    {
        int seed = SchemeRules.SeedOf("refusals");
        SchemeWorld w = SchemeWorldOf();
        SchemeState s = SchemeStateOf(seed, 100, w);
        SchemeShop fresh = s.Shop(SchemePlace, true);
        SchemeShop waiting = new SchemeShop { Place = "Oak~1", ClubDay = 95, ClaimDay = 95 };
        // a club
        Check(SchemeRules.ClubRefusal(true, true, true, false, true, true, fresh, 100) == SchemeRules.Refusal.None
              && SchemeRules.ClubRefusal(false, true, true, false, true, true, fresh, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.ClubRefusal(true, false, true, false, true, true, fresh, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.ClubRefusal(true, true, false, false, true, true, fresh, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.ClubRefusal(true, true, true, true, true, true, fresh, 100) == SchemeRules.Refusal.NoPeople
              && SchemeRules.ClubRefusal(true, true, true, false, false, true, fresh, 100) == SchemeRules.Refusal.NoPeople
              && SchemeRules.ClubRefusal(true, true, true, false, true, false, fresh, 100) == SchemeRules.Refusal.NoPeople
              && SchemeRules.ClubRefusal(true, true, true, false, true, true, waiting, 100) == SchemeRules.Refusal.TooSoon
              && SchemeRules.ClubRefusal(true, true, true, true, true, true, waiting, 100) == SchemeRules.Refusal.NoPeople,
            "schemes refusals: a club is refused in a Going Public session and wherever the city's people are not this machine's own before it is even asked how soon, and for every other want");
        // a claim and an expense
        Check(SchemeRules.ClaimRefusal(true, true, true, true, fresh, 100) == SchemeRules.Refusal.None && SchemeRules.ClaimRefusal(false, true, true, true, fresh, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.ClaimRefusal(true, true, false, true, fresh, 100) == SchemeRules.Refusal.NotNow && SchemeRules.ClaimRefusal(true, true, true, false, fresh, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.ClaimRefusal(true, false, true, true, fresh, 100) == SchemeRules.Refusal.NotNow && SchemeRules.ClaimRefusal(true, true, true, true, waiting, 100) == SchemeRules.Refusal.TooSoon
              && SchemeRules.ExpenseRefusal(true, 20, true, s, 100) == SchemeRules.Refusal.None && SchemeRules.ExpenseRefusal(false, 20, true, s, 100) == SchemeRules.Refusal.NoTax
              && SchemeRules.ExpenseRefusal(true, 0, true, s, 100) == SchemeRules.Refusal.NoTax && SchemeRules.ExpenseRefusal(true, 20, false, s, 100) == SchemeRules.Refusal.NoTax,
            "schemes refusals: an invented claim needs a cover in force, its door and its wait, an invented expense the game's tax falling due");
        s.ExpenseDay = 95;
        Check(SchemeRules.ExpenseRefusal(true, 20, true, s, 100) == SchemeRules.Refusal.TooSoon && SchemeRules.ExpenseRefusal(true, 20, true, s, 95 + SchemeCatalogue.ExpenseGapDays) == SchemeRules.Refusal.None,
            "schemes refusals: an invented expense waits " + SchemeCatalogue.ExpenseGapDays + " days");
        // a fund
        SchemeWorld city = SchemeCityWorld();
        SchemeState fundState = SchemeStateOf(seed, 500, city);
        SchemeRules.FundNow(fundState, 500, city, new List<SchemeEvent>());
        long amount = fundState.Fund.Amount, back = SchemeRules.FundBack(amount), fee = SchemeRules.FundFee(amount, 1000);
        Check(SchemeRules.FundPutRefusal(fundState, 500, false, back, amount + 100, 100) == SchemeRules.Refusal.None
              && SchemeRules.FundPutRefusal(fundState, 500, true, back, amount + 100, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.FundPutRefusal(fundState, 500, false, back - 1, amount + 100, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.FundPutRefusal(fundState, 520, false, back, amount + 100, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.FundPutRefusal(fundState, 500, false, back, amount + 99, 100) == SchemeRules.Refusal.Short
              && SchemeRules.FundLookRefusal(fundState, 500, false, fee, fee + 100, 100) == SchemeRules.Refusal.None
              && SchemeRules.FundLookRefusal(fundState, 500, true, fee, fee + 100, 100) == SchemeRules.Refusal.NotNow
              && SchemeRules.FundLookRefusal(fundState, 500, false, fee, fee + 99, 100) == SchemeRules.Refusal.Short,
            "schemes refusals: money goes into a fund, or a look at it is paid for, only outside a session, while the offer stands, while its rival can pay, and above the investors' floor");
        SchemeRules.FundCheck(fundState);
        Check(SchemeRules.FundLookRefusal(fundState, 500, false, fee, fee + 1000, 100) == SchemeRules.Refusal.NotNow,
            "schemes refusals: a fund is looked at once");
        // paying a file back
        SchemeState file = SchemeStateOf(seed, 100, w);
        SchemeRules.ClubSold(file, SchemePlace, 9000, 50, SchemeCatalogue.Small, 100);
        file.Dirty = 4000;
        SchemeRules.Refusal ok = SchemeRules.RepayRefusal(file, SchemeKind.Club, 101, null, 5000, 0, out long cost, out long wallet);
        SchemeRules.Refusal tight = SchemeRules.RepayRefusal(file, SchemeKind.Club, 101, null, 5000, 1000, out _, out long tightWallet);
        SchemeRules.Refusal nothing = SchemeRules.RepayRefusal(file, SchemeKind.Wash, 101, null, 50000, 0, out long noCost, out _);
        Check(ok == SchemeRules.Refusal.None && cost == 9000 && wallet == 5000 && tight == SchemeRules.Refusal.Short && tightWallet == 5000 && nothing == SchemeRules.Refusal.NotNow && noCost == 0,
            "schemes refusals: paying a file back is refused whole when the wallet's part (the cost less the cash in hand) would go under the investors' floor, and when there is nothing to pay");
        // a favour
        SchemeState people = SchemeStateOf(seed, 100, w);
        string contact = SchemeRules.Contact(3);
        people.Cold.Add(contact);
        Check(SchemeRules.FavourRefusal(people, SchemeRules.Contact(4), 800, false, 5000, 0) == SchemeRules.Refusal.None && SchemeRules.FavourRefusal(people, contact, 800, false, 5000, 0) == SchemeRules.Refusal.NotNow
              && SchemeRules.FavourRefusal(people, SchemeRules.Contact(4), 0, true, 5000, 0) == SchemeRules.Refusal.NotNow
              && SchemeRules.FavourRefusal(people, SchemeRules.Contact(4), 800, false, 799, 0) == SchemeRules.Refusal.Short
              && SchemeRules.FavourRefusal(people, SchemeRules.Contact(4), 800, false, 5000, 4500) == SchemeRules.Refusal.Short
              && SchemeRules.FavourRefusal(people, SchemeRules.Contact(4), 800, true, 0, 4500) == SchemeRules.Refusal.None
              && SchemeRules.NobodyToAsk(Favour.Vote, "seat", true) && !SchemeRules.NobodyToAsk(Favour.Vote, "seat", false) && !SchemeRules.NobodyToAsk(Favour.Gift, "a", true)
              && SchemeRules.NobodyToAsk(Favour.Gift, null, false),
            "schemes refusals: money is not offered to one who is cold to the player, for a favour not priced, past the wallet or the investors' floor, or for a council vote in a Going Public session");
        // what the events move
        var washed = new SchemeEvent { Kind = SchemeEventKind.Washed, Amount = 600, Net = 408 };
        var caution = new SchemeEvent { Kind = SchemeEventKind.Caution, Net = 90 };
        var acted = new SchemeEvent { Kind = SchemeEventKind.RivalActed, Amount = 4000 };
        var found = new SchemeEvent { Kind = SchemeEventKind.RivalFound, Amount = 5000 };
        Check(SchemeRules.WalletOf(washed) == 408 && SchemeRules.WalletOf(caution) == -90 && SchemeRules.WalletOf(new SchemeEvent { Kind = SchemeEventKind.FundRecovered, Amount = 7 }) == 7
              && SchemeRules.WalletOf(new SchemeEvent { Kind = SchemeEventKind.Letter, Net = 5, Amount = 5 }) == 0 && SchemeRules.RivalMove(acted) == 4000 && SchemeRules.RivalMove(found) == -5000
              && SchemeRules.RivalMove(washed) == 0 && SchemeRules.RivalCharge(SchemeKind.Graft) == JusticeLaw.Bribery && SchemeRules.RivalCharge(SchemeKind.Expense) == JusticeLaw.TaxEvasion
              && SchemeRules.RivalCharge(SchemeKind.Club) == JusticeLaw.Fraud,
            "schemes refusals: what an event moves in the wallet and in a rival's worth, and the charge a rival answers, are one rule for the director and the simulations");
        SchemeRules.Due due = SchemeRules.FindingsDue(SchemeKind.Wash, 800, 300, 200), claim = SchemeRules.FindingsDue(SchemeKind.Claim, 800, 0, 200);
        Check(due.Wallet == 500 && due.Surcharge == 200 && due.Households == 800 && claim.Wallet == 800 && claim.Households == 0 && SchemeRules.FindingsDue(SchemeKind.Club, 0, 0, 0).Households == 0
              && SchemeRules.FindingsDue(SchemeKind.Club, 100, 400, 0).Wallet == 0,
            "schemes refusals: the findings take what the cash in hand did not cover from the wallet whatever it holds, and give a club's or a till's takings back to the households");
    }

    // ------------------------------------------------------------------------------------------------- each answer reaches its call

    private sealed class SchemeRecorder : ISchemeActs
    {
        public readonly List<string> Calls = new List<string>();
        public void Claim(string place, int cover, int size) => Calls.Add("Claim " + place + " " + cover + " " + size);
        public void Expense(int size) => Calls.Add("Expense " + size);
        public void OpenLift(string place) => Calls.Add("OpenLift " + place);
        public void OpenWash(string place) => Calls.Add("OpenWash " + place);
        public void Lift(string place, int permille) => Calls.Add("Lift " + place + " " + permille);
        public void Wash(string place, int permille) => Calls.Add("Wash " + place + " " + permille);
        public void Club(string place, int size) => Calls.Add("Club " + place + " " + size);
        public void FundPut() => Calls.Add("FundPut");
        public void FundLook() => Calls.Add("FundLook");
        public void FundDown() => Calls.Add("FundDown");
    }

    private static void SchemeDispatchChecks()
    {
        var wanted = new Dictionary<SchemeDo, string>
        {
            { SchemeDo.Claim, "Claim Elm~7 1 1000" }, { SchemeDo.Expense, "Expense 250" }, { SchemeDo.OpenLift, "OpenLift Elm~7" }, { SchemeDo.OpenWash, "OpenWash Elm~7" },
            { SchemeDo.Lift, "Lift Elm~7 300" }, { SchemeDo.Wash, "Wash Elm~7 200" }, { SchemeDo.Club, "Club Elm~7 250" }, { SchemeDo.FundPut, "FundPut" }, { SchemeDo.FundLook, "FundLook" },
            { SchemeDo.FundDown, "FundDown" }
        };
        var press = new Dictionary<SchemeDo, SchemePress>
        {
            { SchemeDo.Claim, SchemePress.Of(SchemeDo.Claim, SchemePlace, 1, 1000) }, { SchemeDo.Expense, SchemePress.Of(SchemeDo.Expense, null, 0, 250) },
            { SchemeDo.OpenLift, SchemePress.Of(SchemeDo.OpenLift, SchemePlace) }, { SchemeDo.OpenWash, SchemePress.Of(SchemeDo.OpenWash, SchemePlace) },
            { SchemeDo.Lift, SchemePress.Of(SchemeDo.Lift, SchemePlace, 300) }, { SchemeDo.Wash, SchemePress.Of(SchemeDo.Wash, SchemePlace, 200) },
            { SchemeDo.Club, SchemePress.Of(SchemeDo.Club, SchemePlace, 0, 250) }, { SchemeDo.FundPut, SchemePress.Of(SchemeDo.FundPut) }, { SchemeDo.FundLook, SchemePress.Of(SchemeDo.FundLook) },
            { SchemeDo.FundDown, SchemePress.Of(SchemeDo.FundDown) }
        };
        foreach (SchemeDo what in Enum.GetValues(typeof(SchemeDo)))
        {
            var recorder = new SchemeRecorder();
            (what == SchemeDo.Leave ? SchemePress.Of(SchemeDo.Leave, SchemePlace, 1, 1) : press[what]).Carry(recorder);
            if (what == SchemeDo.Leave)
            {
                Check(recorder.Calls.Count == 0, "schemes pages: leaving a page does nothing");
                continue;
            }
            Check(wanted.ContainsKey(what) && recorder.Calls.Count == 1 && recorder.Calls[0] == wanted[what],
                "schemes pages: the answer " + what + " reaches its call with its place and figures (" + string.Join(" | ", recorder.Calls) + ")");
        }
    }
}
