using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The four licence kinds added to the Company app (a fire safety certificate, an outdoor seating permit, a delivery vehicle permit, bonded storage): which
// business needs each, the fee, the days and the city's decision on an application, what a lapse costs (the fire certificate's link to the fire cover), and the
// save, both ways round and from a line written before they existed.
internal static partial class Program
{
    // The sixteen fields the four licences added later come to at the end of a business's record when none is held or applied for.
    private const string NoNewLicences = "|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0";

    // The same sixteen fields for a business as it is.
    private static string NewLicencesTail(ShopFile s)
    {
        string tail = string.Empty;
        for (int k = CompanyCatalogue.LicenceOwnKinds; k < s.Permits.Length; k++)
        {
            tail += "|" + s.Permits[k].Until + "|" + (s.Permits[k].Auto ? 1 : 0) + "|" + (s.Permits[k].Reminded ? 1 : 0) + "|" + s.Permits[k].Decide;
        }
        return tail;
    }

    private static void CompanyLicenceKinds()
    {
        int before = _run, failedBefore = _failed;
        LicenceKindsAre();
        LicenceNeedsAre();
        LicenceApplications();
        LicenceFirstTerms();
        LicenceFireCover();
        LicenceSaves();
        Console.WriteLine("  licence kinds, applications and the fire certificate's link to the cover: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private static void LicenceKindsAre()
    {
        int kinds = CompanyCatalogue.LicenceKinds;
        Check(kinds == 8 && Enum.GetValues(typeof(LicenceKind)).Length == kinds && new ShopFile().Permits.Length == kinds, "licence kinds: eight, in the catalogue, the enum and a business's file");
        Check(CompanyCatalogue.LicenceFee.Length == kinds && CompanyCatalogue.LicenceTerm.Length == kinds && CompanyCatalogue.LicenceDecide.Length == kinds,
            "licence kinds: a fee, a term and a decision time for each");
        bool sane = true;
        for (int k = 0; k < kinds; k++)
        {
            sane &= CompanyCatalogue.LicenceFee[k] > 0 && CompanyCatalogue.LicenceTerm[k] >= CompanyCatalogue.LicenceRemindDays * 2;
            sane &= k < CompanyCatalogue.LicenceOwnKinds ? CompanyCatalogue.LicenceDecide[k] == 0 : CompanyCatalogue.LicenceDecide[k] >= 2 && CompanyCatalogue.LicenceDecide[k] <= 5;
        }
        Check(sane, "licence kinds: every fee and term above nothing, the first four granted at once, the four added decided by the city in two to five days");
        Check((int)LicenceKind.FireSafety == 4 && (int)LicenceKind.OutdoorSeating == 5 && (int)LicenceKind.DeliveryVehicle == 6 && (int)LicenceKind.BondedStorage == 7
              && (int)LicenceKind.Hygiene == 0 && (int)LicenceKind.Signage == 3, "licence kinds: the new ones are numbered after the old, which keep their numbers (they are saved as numbers)");
        Check(LicenceRules.Fee((int)LicenceKind.BondedStorage, 1000) == CompanyCatalogue.AtPrices(CompanyCatalogue.LicenceFee[7], 1000) && LicenceRules.Fine(4, 1000) == 2 * LicenceRules.Fee(4, 1000),
            "licence kinds: the fee follows the city's prices, the fine for trading without one is twice the fee");
        Check(!LicenceRules.Shuts((int)LicenceKind.FireSafety) && !LicenceRules.Shuts((int)LicenceKind.OutdoorSeating) && LicenceRules.Shuts((int)LicenceKind.Hygiene),
            "licence kinds: only food hygiene and alcohol shut the doors; the new ones are fined and, for fire, felt in the cover");
    }

    private static void LicenceNeedsAre()
    {
        int signage = 1 << (int)LicenceKind.Signage;
        Check(LicenceRules.Needs(new LicenceSite()) == signage, "licence needs: a plain shop needs a sign and nothing more");
        Check(LicenceRules.Needs(new LicenceSite { Food = true }) == (signage | 1 << (int)LicenceKind.Hygiene), "licence needs: a shop that sells food without tables needs hygiene, not fire safety or seating");
        int cafe = LicenceRules.Needs(new LicenceSite { Food = true, Seating = true });
        Check(LicenceRules.Needed(cafe, (int)LicenceKind.FireSafety) && LicenceRules.Needed(cafe, (int)LicenceKind.OutdoorSeating) && LicenceRules.Needed(cafe, (int)LicenceKind.Hygiene)
              && !LicenceRules.Needed(cafe, (int)LicenceKind.Alcohol) && !LicenceRules.Needed(cafe, (int)LicenceKind.DeliveryVehicle) && !LicenceRules.Needed(cafe, (int)LicenceKind.BondedStorage),
            "licence needs: tables bring fire safety and outdoor seating on top of hygiene");
        int club = LicenceRules.Needs(new LicenceSite { Alcohol = true, Late = true, Crowd = true });
        Check(LicenceRules.Needed(club, (int)LicenceKind.FireSafety) && LicenceRules.Needed(club, (int)LicenceKind.Alcohol) && LicenceRules.Needed(club, (int)LicenceKind.Late)
              && !LicenceRules.Needed(club, (int)LicenceKind.OutdoorSeating), "licence needs: a crowded room needs fire safety, with its drink and late hours, but no pavement tables");
        int van = LicenceRules.Needs(new LicenceSite { Drivers = true });
        Check(LicenceRules.Needed(van, (int)LicenceKind.DeliveryVehicle) && !LicenceRules.Needed(van, (int)LicenceKind.FireSafety), "licence needs: a delivery driver on the staff brings the delivery vehicle permit");
        int cellar = LicenceRules.Needs(new LicenceSite { Alcohol = true, Cellar = true });
        Check(LicenceRules.Needed(cellar, (int)LicenceKind.BondedStorage) && LicenceRules.Needed(cellar, (int)LicenceKind.Alcohol), "licence needs: goods held in bulk bring bonded storage");
        Check(LicenceRules.Needs(true, false, false) == LicenceRules.Needs(new LicenceSite { Food = true }) && LicenceRules.Needs(true, true, true) == LicenceRules.Needs(new LicenceSite { Food = true, Alcohol = true, Late = true }),
            "licence needs: the first version's three questions still give the same answer");
        int all = LicenceRules.Needs(new LicenceSite { Food = true, Alcohol = true, Late = true, Crowd = true, Seating = true, Drivers = true, Cellar = true });
        Check(all == (1 << CompanyCatalogue.LicenceKinds) - 1, "licence needs: a business that is everything needs all eight, and the eight fit in the mask (" + all + ")");
    }

    private static void LicenceApplications()
    {
        const int fire = (int)LicenceKind.FireSafety, bonded = (int)LicenceKind.BondedStorage, signage = (int)LicenceKind.Signage;
        var l = new Licence();
        Check(LicenceRules.CanApply(l) && !l.Pending && LicenceRules.DaysToDecision(l, 10) == 0, "licence application: a permit not held, not applied for, may be applied for");
        bool waits = LicenceRules.Apply(l, fire, 100);
        Check(waits && l.Pending && l.Decide == 100 + CompanyCatalogue.LicenceDecide[fire] && !LicenceRules.Valid(l, 100) && l.Until == 0,
            "licence application: a permit not held waits for the city's decision, " + CompanyCatalogue.LicenceDecide[fire] + " days for fire safety, and is not yet valid");
        Check(LicenceRules.Covered(l, 100) && LicenceRules.DaysToDecision(l, 100) == 3 && LicenceRules.DaysToDecision(l, 102) == 1 && !LicenceRules.CanApply(l),
            "licence application: until it is decided it counts as held, shows the days left and may not be applied for twice");
        var shop = new ShopFile();
        shop.Permits[fire] = l;
        int needs = LicenceRules.Needs(new LicenceSite { Seating = true });
        Check(LicenceRules.Missing(shop, needs, 100) == ((1 << (int)LicenceKind.OutdoorSeating) | (1 << (int)LicenceKind.Signage)),
            "licence application: an inspector does not count an application in hand as missing (the others still are)");
        Check(LicenceRules.Day(l, 101) == LicenceNote.None && LicenceRules.Day(l, 102) == LicenceNote.None && LicenceRules.Day(l, 103) == LicenceNote.Granted,
            "licence application: nothing happens until the decision day, and then it is granted");
        LicenceRules.Grant(l, fire, 103);
        Check(!l.Pending && l.Decide == 0 && l.Until == 103 - 1 + CompanyCatalogue.LicenceTerm[fire] && LicenceRules.Valid(l, 103) && LicenceRules.Valid(l, l.Until) && !LicenceRules.Valid(l, l.Until + 1),
            "licence application: granted, it runs for its whole term from the decision day, and nothing is left pending");
        Check(LicenceRules.Day(l, 104) == LicenceNote.None && LicenceRules.CanApply(l), "licence application: after it is granted the day brings no second decision");
        // Renewing one still running is at once: a term added at the end, never made to wait.
        var held = new Licence { Until = 140 };
        bool waited = LicenceRules.Apply(held, fire, 130);
        Check(!waited && held.Until == 140 + CompanyCatalogue.LicenceTerm[fire] && !held.Pending, "licence application: a permit still running is renewed at once, a term added at its end");
        // One that ran out must be applied for again.
        var lapsed = new Licence { Until = 120 };
        Check(LicenceRules.Apply(lapsed, bonded, 130) && lapsed.Decide == 130 + CompanyCatalogue.LicenceDecide[bonded] && lapsed.Until == 120,
            "licence application: a permit that ran out is applied for again and waits (" + CompanyCatalogue.LicenceDecide[bonded] + " days for bonded storage)");
        // The first four are bought at once, as before.
        var old = new Licence();
        Check(!LicenceRules.Apply(old, signage, 50) && old.Until == 49 + CompanyCatalogue.LicenceTerm[signage] && !old.Pending, "licence application: the first four kinds are bought at once, as they always were");
        // A day's note for a pending application over a lapsed permit: the decision comes first.
        var both = new Licence { Until = 99, Decide = 105 };
        Check(LicenceRules.Day(both, 104) == LicenceNote.None || LicenceRules.Day(both, 104) == LicenceNote.Lapsed || LicenceRules.Day(both, 104) == LicenceNote.Remind, "licence application: the days before a decision give only the ordinary notes");
        Check(LicenceRules.Day(both, 105) == LicenceNote.Granted, "licence application: the decision day grants it, whatever the old permit did");
        // Auto-renewal of a kind that waits: a permit that renews by itself does so at once (it was applied for once).
        var auto = new Licence { Until = 200, Auto = true };
        Check(LicenceRules.Day(auto, 201) == LicenceNote.Renew, "licence application: a permit on auto-renew renews at once the day after it runs out, with no wait");
        // Many applications in a year: no application ever has a decision before it is made.
        bool never = true;
        for (int kind = 0; kind < CompanyCatalogue.LicenceKinds; kind++)
        {
            for (int day = 1; day < 400; day += 37)
            {
                var x = new Licence();
                LicenceRules.Apply(x, kind, day);
                never &= !x.Pending || x.Decide > day;
                never &= x.Pending == (CompanyCatalogue.LicenceDecide[kind] > 0);
            }
        }
        Check(never, "licence application: a decision is always after the application, and exactly the kinds with a decision time wait");
    }

    private static void LicenceFirstTerms()
    {
        var shop = new ShopFile();
        int needs = LicenceRules.Needs(new LicenceSite { Seating = true, Food = true });
        // A business that ran before the kinds were asked for: the old kinds are held, the new ones are given a full first term when first seen to be needed.
        for (int k = 0; k < CompanyCatalogue.LicenceOwnKinds; k++)
        {
            shop.Permits[k].Until = 200;
        }
        int granted = LicenceRules.Newly(shop, needs, 150);
        int fire = (int)LicenceKind.FireSafety, seating = (int)LicenceKind.OutdoorSeating;
        Check(granted == (1 << fire | 1 << seating) && shop.Permits[fire].Until == 149 + CompanyCatalogue.LicenceTerm[fire] && shop.Permits[seating].Until == 149 + CompanyCatalogue.LicenceTerm[seating],
            "licence first term: kinds added later start with a full free term where a business first needs them, not a fine");
        Check(LicenceRules.Newly(shop, needs, 151) == 0 && shop.Permits[fire].Until == 149 + CompanyCatalogue.LicenceTerm[fire], "licence first term: given once; it does not come back the next day");
        // A kind of the first four that is newly needed has the grace of a few days only.
        var grace = new ShopFile();
        int signed = LicenceRules.Newly(grace, 1 << (int)LicenceKind.Late, 60);
        Check(signed == 1 << (int)LicenceKind.Late && grace.Permits[(int)LicenceKind.Late].Until == 59 + CompanyCatalogue.LicenceGraceDays, "licence first term: the first four kinds newly needed keep their few days' grace");
        // A new business opened after the feature: grace for all, a full term when the feature is new to the save.
        var fresh = new ShopFile();
        LicenceRules.First(fresh, needs, 300, false);
        Check(fresh.Permits[fire].Until == 299 + CompanyCatalogue.LicenceGraceDays && fresh.Permits[seating].Until == 299 + CompanyCatalogue.LicenceGraceDays, "licence first term: a new business has a few days' grace for every permit it needs");
        var ran = new ShopFile();
        LicenceRules.First(ran, needs, 300, true);
        Check(ran.Permits[fire].Until == 299 + CompanyCatalogue.LicenceTerm[fire], "licence first term: a business that ran before the feature began holds a full first term of each");
        // A waiting application is not overwritten by a first term.
        var waiting = new ShopFile();
        waiting.Permits[fire].Decide = 310;
        LicenceRules.Newly(waiting, needs, 300);
        Check(waiting.Permits[fire].Until == 0 && waiting.Permits[fire].Decide == 310, "licence first term: a permit already applied for is left to the city");
    }

    private static void LicenceFireCover()
    {
        const int fire = (int)CoverKind.Fire, theft = (int)CoverKind.Theft, liability = (int)CoverKind.Liability;
        int plain = CoverRules.Premium(fire, 1, 3000, 100, 0, 1000);
        int certified = CoverRules.Premium(fire, 1, 3000, 100, 0, 1000, true);
        int explicitFalse = CoverRules.Premium(fire, 1, 3000, 100, 0, 1000, false);
        Check(plain == explicitFalse && plain > 0, "fire certificate: with none, the premium is the one it always was (" + plain + ")");
        Check(certified < plain && Math.Abs(certified - plain * (1000 - CompanyCatalogue.FireCoverCut) / 1000) <= 1, "fire certificate: one in force takes " + CompanyCatalogue.FireCoverCut / 10 + "% off the fire premium (" + plain + " to " + certified + ")");
        Check(CoverRules.Premium(theft, 1, 3000, 100, 0, 1000, true) == CoverRules.Premium(theft, 1, 3000, 100, 0, 1000) && CoverRules.Premium(liability, 1, 3000, 100, 0, 1000, true) == CoverRules.Premium(liability, 1, 3000, 100, 0, 1000),
            "fire certificate: theft and liability cover cost the same with one");
        bool never = true;
        for (int insurer = 0; insurer < CompanyCatalogue.Insurers; insurer++)
        {
            for (int takings = 0; takings <= 20000; takings += 1000)
            {
                never &= CoverRules.Premium(fire, insurer, takings, 100, 1, 1000, true) >= 1 && CoverRules.Premium(fire, insurer, takings, 100, 1, 1000, true) <= CoverRules.Premium(fire, insurer, takings, 100, 1, 1000);
            }
        }
        Check(never, "fire certificate: with one the premium is never above the other, and never below a pound a day");
        // Renewing a fire policy counts the certificate in force at the renewal.
        var a = new Policy();
        CoverRules.Buy(a, 1, 50, 10, 60);
        var b = new Policy();
        CoverRules.Buy(b, 1, 50, 10, 60);
        CoverRules.Renew(a, fire, 3000, 100, 1000, 70, 60, false);
        CoverRules.Renew(b, fire, 3000, 100, 1000, 70, 60, true);
        Check(b.Premium < a.Premium, "fire certificate: a policy renewed with one in force is cheaper (" + a.Premium + " to " + b.Premium + ")");
        // A fire claim at a business whose certificate has run out pays half; theft is not touched.
        var state = new CompanyState();
        var policy = new Policy();
        CoverRules.Buy(policy, 1, 50, 10, 60);
        Claim full = CoverRules.Open(state, policy, "P~1", "Cafe", fire, 5000, 20);
        Claim half = CoverRules.Open(state, policy, "P~1", "Cafe", fire, 5000, 20, true);
        Check(full != null && half != null && half.Payout == CompanyCatalogue.Share(full.Payout, CompanyCatalogue.FireClaimShare) && half.Payout < full.Payout,
            "fire certificate: a fire claim where the certificate the business needs has run out pays " + CompanyCatalogue.FireClaimShare / 10 + "% of what it would (" + full.Payout + " to " + half.Payout + ")");
        Claim theftFull = CoverRules.Open(state, policy, "P~1", "Cafe", theft, 2000, 21);
        Claim theftFlagged = CoverRules.Open(state, policy, "P~1", "Cafe", theft, 2000, 21, true);
        Check(theftFull != null && theftFlagged != null && theftFull.Payout == theftFlagged.Payout, "fire certificate: a theft claim is never reduced for it");
        // Excess and the loss are kept as they were; only the payout is cut.
        Check(half.Excess == full.Excess && half.Loss == full.Loss, "fire certificate: the claim records the same loss and excess, only the payout is cut");
    }

    private static void LicenceSaves()
    {
        var s = new CompanyState { Seed = 77, FirstDay = 5, LastDay = 300, Age = 295 };
        ShopFile shop = new ShopFile { Place = "Main~1", FirstDay = 5 };
        shop.Permits[0].Until = 330;
        shop.Permits[1].Until = 331;
        shop.Permits[1].Auto = true;
        shop.Permits[3].Until = 340;
        shop.Permits[3].Reminded = true;
        shop.Permits[4].Until = 360;
        shop.Permits[4].Auto = true;
        shop.Permits[5].Decide = 306;
        shop.Permits[6].Until = 350;
        shop.Permits[6].Reminded = true;
        shop.Permits[7].Until = 380;
        shop.Permits[7].Decide = 0;
        shop.Push = (int)CampaignKind.Flyers;   // a campaign running, so that what it gave away is kept (the reader keeps it only for one)
        shop.PushFrom = 10;
        shop.PushEnd = 13;
        shop.PushCost = 500;
        shop.PushPercent = 5;
        shop.PushGiven = 12;
        s.Shops.Add(shop);
        string line = CompanyStore.Write(s);
        int version;
        CompanyState back = CompanyStore.Read(line, out version);
        ShopFile r = back != null && back.Shops.Count == 1 ? back.Shops[0] : null;
        bool same = r != null;
        for (int k = 0; same && k < CompanyCatalogue.LicenceKinds; k++)
        {
            same &= r.Permits[k].Until == shop.Permits[k].Until && r.Permits[k].Auto == shop.Permits[k].Auto && r.Permits[k].Reminded == shop.Permits[k].Reminded && r.Permits[k].Decide == shop.Permits[k].Decide;
        }
        Check(same && r.PushGiven == 12, "licence save: all eight permits of a business, their terms, renewals, reminders and waiting applications, round trip");
        Check(back != null && CompanyStore.Write(back) == line, "licence save: what is read back writes the same line again");
        // The first four are where the first version put them: an older reader finds them there.
        string[] parts = line.Split('^');
        string record = Array.Find(parts, p => p.StartsWith("s:", StringComparison.Ordinal)).Substring(2);
        string[] f = record.Split('|');
        int at = 21 + 3 * 7;   // the first field of the first licence: after the shop's own figures and its three policies
        Check(f[at] == "330" && f[at + 3] == "331" && f[at + 4] == "1" && f[at + 9] == "340" && f[at + 11] == "1", "licence save: the first four permits are in the places the first version wrote them, so an older version reads them");
        // A line from before the new kinds: the same record with the four added kinds' sixteen fields cut off the end.
        string cut = string.Join("|", f, 0, f.Length - 16);
        string older = line.Replace(record, cut);
        CompanyState from = CompanyStore.Read(older, out version);
        ShopFile o = from != null && from.Shops.Count == 1 ? from.Shops[0] : null;
        bool kept = o != null && o.Permits[0].Until == 330 && o.Permits[1].Auto && o.Permits[3].Reminded && o.PushGiven == 12;
        for (int k = CompanyCatalogue.LicenceOwnKinds; o != null && k < CompanyCatalogue.LicenceKinds; k++)
        {
            kept &= o.Permits[k].Until == 0 && !o.Permits[k].Auto && !o.Permits[k].Reminded && !o.Permits[k].Pending;
        }
        Check(kept, "licence save: a line written before the new kinds reads with the first four intact and the new ones held by nobody");
        // Garbage in the new fields is bounded, not trusted.
        string wild = string.Join("|", f, 0, f.Length - 16) + "|-5|7|7|-9|9999|1|0|99999|0|0|0|0|0|0|0|3";
        CompanyState bad = CompanyStore.Read(line.Replace(record, wild), out version);
        ShopFile w = bad != null && bad.Shops.Count == 1 ? bad.Shops[0] : null;
        Check(w != null && w.Permits[4].Until == 0 && w.Permits[4].Decide == 0 && w.Permits[5].Until == 9999 && w.Permits[5].Decide == 99999 && w.Permits[7].Decide == 3 && w.Permits[7].Until == 0,
            "licence save: a negative day reads as none; the fields are bounded as the rest of the line is");
        // Several businesses: each keeps its own.
        var many = new CompanyState { Seed = 1, LastDay = 10 };
        for (int i = 0; i < 5; i++)
        {
            var sf = new ShopFile { Place = "P" + i + "~" + i };
            sf.Permits[4 + i % 4].Decide = 20 + i;
            sf.Permits[4 + (i + 1) % 4].Until = 100 + i;
            many.Shops.Add(sf);
        }
        CompanyState manyBack = CompanyStore.Read(CompanyStore.Write(many), out version);
        bool each = manyBack != null && manyBack.Shops.Count == 5;
        for (int i = 0; each && i < 5; i++)
        {
            each &= manyBack.Shops[i].Permits[4 + i % 4].Decide == 20 + i && manyBack.Shops[i].Permits[4 + (i + 1) % 4].Until == 100 + i;
        }
        Check(each, "licence save: several businesses keep their own applications and terms");
        // The Planner's row for a permit running out has an identity of its own for each business and kind (the old four-kind form would have shared them).
        var ids = new HashSet<int>();
        bool distinct = true;
        for (int number = 1; number <= 300; number++)
        {
            for (int k = 0; k < CompanyCatalogue.LicenceKinds; k++)
            {
                distinct &= ids.Add(number % 100000000 * CompanyCatalogue.LicenceKinds + k);
            }
        }
        Check(distinct, "licence save: the Planner's row for a permit running out is distinct for every business and every one of the eight kinds");
    }
}
