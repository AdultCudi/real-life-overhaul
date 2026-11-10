using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Dynasty;
using RealLifeOverhaul.Ui;

// The dynasty (Scripts/Dynasty, run with --dynasty): the tax and every band, the gifts' window (no way round it by timing, by splitting or by the option), a will's terms and
// its fee, who carries on, an estate settled to the last unit of money over thousands of seeded estates (debts first, the investors' floor kept, charities and a married
// partner's part exempt within their bounds, the tax never cut), the debt for tax, the retired founder's fund counted with the estate they handed over, what holds a handover
// back and what refuses retiring, a handover in its steps with a save and a load between every two, the rivals' lines over two centuries, the city's estates, the heir's
// learning, the save (old saves, a newer line, a culture with decimal commas, names that try to break a record), the pages and every button, the sources and the texts.
// Every rule here is the shipped one, called as the director calls it.
internal static partial class Program
{
    private static readonly string[] DynastyLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static void Dynasty()
    {
        DynastyTaxChecks();
        DynastyGiftChecks();
        DynastyWillChecks();
        DynastyHeirChecks();
        DynastySettleChecks();
        DynastyExploitChecks();
        DynastyGateChecks();
        DynastyHandChecks();
        DynastyLoanChecks();
        DynastyElderChecks();
        DynastyRivalChecks();
        DynastyCityChecks();
        DynastyStudyChecks();
        DynastyStoreChecks();
        DynastyPageChecks();
        DynastySourceChecks();
        DynastyTextChecks();
    }

    // ---- the tax

    private static void DynastyTaxChecks()
    {
        const int level = 1000;
        long free = DynastyCatalogue.Threshold, low = DynastyCatalogue.BandWidth[0], mid = DynastyCatalogue.BandWidth[1];
        Check(DynastyRules.Tax(0, level) == 0 && DynastyRules.Tax(free, level) == 0 && DynastyRules.Tax(-5000, level) == 0, "dynasty tax: nothing up to the threshold");
        Check(DynastyRules.Tax(free + 1000, level) == 1000 * DynastyCatalogue.BandRate[0] / 1000, "dynasty tax: the first band's rate just over the threshold");
        Check(DynastyRules.Tax(free + low, level) == low * DynastyCatalogue.BandRate[0] / 1000, "dynasty tax: the whole first band (" + DynastyRules.Tax(free + low, level) + ")");
        long two = low * DynastyCatalogue.BandRate[0] / 1000 + mid * DynastyCatalogue.BandRate[1] / 1000;
        Check(DynastyRules.Tax(free + low + mid, level) == two, "dynasty tax: the first two bands (" + two + ")");
        Check(DynastyRules.Tax(free + low + mid + 10000000, level) == two + 10000000L * DynastyCatalogue.BandRate[2] / 1000, "dynasty tax: the top rate on the rest");
        Check(DynastyCatalogue.BandRate.Length == DynastyCatalogue.BandWidth.Length + 1 && DynastyCatalogue.BandRate[0] < DynastyCatalogue.BandRate[1]
              && DynastyCatalogue.BandRate[1] < DynastyCatalogue.BandRate[2] && DynastyCatalogue.BandRate[2] == 400, "dynasty tax: the bands climb to a top rate of 40%");
        bool rising = true, under = true;
        long before = 0;
        for (long x = 0; x <= 60000000; x += 173211)
        {
            long t = DynastyRules.Tax(x, level);
            rising &= t >= before && t - before <= 173211L * DynastyCatalogue.BandRate[2] / 1000 + 1;
            under &= t <= x * DynastyCatalogue.BandRate[2] / 1000;
            before = t;
        }
        Check(rising, "dynasty tax: more estate never means less tax, and no step of it is taxed over the top rate");
        Check(under, "dynasty tax: the tax is never over the top rate of the whole");
        Check(DynastyRules.Tax(free * 2, 2000) == 0 && DynastyRules.Tax(free * 2 + 2000, 2000) == 2000L * DynastyCatalogue.BandRate[0] / 1000,
            "dynasty tax: the threshold follows the city's prices");
        bool once = true;
        for (int i = 0; i < 400; i++)
        {
            long a = DynastyRules.Hash(7, 1, i, 0) % 9000000u, b = DynastyRules.Hash(7, 2, i, 0) % 9000000u;
            once &= DynastyRules.Tax(a, level) + DynastyRules.TaxOn(b, a, level) == DynastyRules.Tax(a + b, level);
        }
        Check(once, "dynasty tax: an estate in two parts bears what it would in one: the threshold and the low bands are used once");
        Check(DynastyRules.TaxOn(500000, 0, level) == 0 && DynastyRules.TaxOn(500000, free, level) == 500000L * DynastyCatalogue.BandRate[0] / 1000,
            "dynasty tax: a later part is taxed from where the earlier one ended");
    }

    // ---- gifts in life

    private static void DynastyGiftChecks()
    {
        const int year = 60;
        int window = DynastyRules.WindowDays(year);
        Check(window == DynastyCatalogue.WindowYears * year && DynastyCatalogue.WindowYears == 3, "dynasty gifts: the window is three of the game's years (" + window + " days)");
        var gifts = new List<LifeGift>();
        DynastyRules.Gift(gifts, 100, 5000);
        Check(DynastyRules.GiftsInWindow(gifts, 100, year) == 5000 && DynastyRules.GiftsInWindow(gifts, 100 + window - 1, year) == 5000,
            "dynasty gifts: a gift counts from its own day to the last day of the window");
        Check(DynastyRules.GiftsInWindow(gifts, 100 + window, year) == 0, "dynasty gifts: a gift past the window is not counted");
        var many = new List<LifeGift>();
        for (int i = 0; i < 50; i++)
        {
            DynastyRules.Gift(many, 200 + i / 10, 1000);
        }
        Check(DynastyRules.GiftsInWindow(many, 210, year) == 50000 && many.Count == 5, "dynasty gifts: many small gifts count as their sum, a day's gifts as one line");
        DynastyRules.Gift(many, 210, 0);
        DynastyRules.Gift(many, 210, -40);
        Check(DynastyRules.GiftsInWindow(many, 210, year) == 50000, "dynasty gifts: nothing and less than nothing are no gift");
        var flood = new List<LifeGift>();
        long sum = 0;
        for (int d = 1; d <= DynastyCatalogue.GiftsKept + 90; d++)
        {
            DynastyRules.Gift(flood, d, 10 + d);
            sum += 10 + d;
        }
        long kept = flood.Sum(g => g.Amount);
        Check(flood.Count == DynastyCatalogue.GiftsKept && kept == sum, "dynasty gifts: past the most kept nothing is dropped (" + kept + " of " + sum + ")");
        Check(flood[0].Day >= 91 && flood[0].Amount > 10 + flood[0].Day, "dynasty gifts: the oldest are counted on a later day, never an earlier one");
        var old = new List<LifeGift> { new LifeGift { Day = 10, Amount = 7 }, new LifeGift { Day = 400, Amount = 9 } };
        DynastyRules.ForgetOld(old, 10 + window, year);
        Check(old.Count == 1 && old[0].Amount == 9, "dynasty gifts: a gift that has left the window is let go");
    }

    // ---- the will

    private static Will DynastyWillSample()
    {
        var w = new Will { Written = true, HeirKind = KinKind.Child, HeirName = "Tom", RetireAge = 60, Keep = 1 };
        w.Bequests.Add(new Bequest { Kind = KinKind.Partner, Name = "Maya", Share = 200 });
        w.Bequests.Add(new Bequest { Kind = KinKind.Charity, Id = 1, Name = "Trust", Share = 100 });
        w.Bequests.Add(new Bequest { Kind = KinKind.Person, Id = 4, Name = "Nadia", Share = 50 });
        return w;
    }

    private static void DynastyWillChecks()
    {
        Will w = DynastyWillSample();
        Check(DynastyRules.Valid(w) == WillWhy.Ok && w.Others() == 350, "dynasty will: a will of three bequests stands");
        Will over = w.Copy();
        over.Bequests[0].Share = 400;
        Check(DynastyRules.Valid(over) == WillWhy.Over && DynastyCatalogue.OthersMost == 500, "dynasty will: all bequests together cannot take more than half (the heir's reserved part)");
        Will odd = w.Copy();
        odd.Bequests[1].Share = 75;
        Check(DynastyRules.Valid(odd) == WillWhy.Share, "dynasty will: a share off its steps does not stand");
        Will self = w.Copy();
        self.Bequests.Add(new Bequest { Kind = KinKind.Child, Name = "Tom", Share = 50 });
        Check(DynastyRules.Valid(self) == WillWhy.Heir, "dynasty will: the heir is left nothing by name");
        Will twice = w.Copy();
        twice.Bequests.Add(new Bequest { Kind = KinKind.Person, Id = 4, Name = "Nadia", Share = 50 });
        Check(DynastyRules.Valid(twice) == WillWhy.Heir, "dynasty will: nobody is named twice");
        Will age = w.Copy();
        age.RetireAge = 54;
        Will age2 = w.Copy();
        age2.RetireAge = 62;
        Will keep = w.Copy();
        keep.Keep = DynastyCatalogue.KeepSteps.Length;
        Check(DynastyRules.Valid(age) == WillWhy.Age && DynastyRules.Valid(age2) == WillWhy.Age && DynastyRules.Valid(keep) == WillWhy.Keep,
            "dynasty will: the retiring age and what is kept stay inside their steps");
        var many = new Will { Written = true };
        for (int i = 0; i < DynastyCatalogue.BequestsMost + 1; i++)
        {
            many.Bequests.Add(new Bequest { Kind = KinKind.Person, Id = i, Name = "P" + i, Share = 50 });
        }
        Check(DynastyRules.Valid(many) == WillWhy.TooMany, "dynasty will: no more bequests than the will holds");
        DynastyRules.Tidy(many);
        Check(many.Bequests.Count == DynastyCatalogue.BequestsMost && DynastyRules.Valid(many) == WillWhy.Ok, "dynasty will: a will read from a save loses only the bequests that break it");

        var draft = new Will();
        DynastyWill.SetHeir(draft, KinKind.Child, 9, "Tom");
        Check(draft.HeirKind == KinKind.Child && draft.HeirId == 0 && draft.HeirName == "Tom", "dynasty will: an heir is named by the draft");
        for (int i = 0; i < 30; i++)
        {
            DynastyWill.Move(draft, KinKind.Partner, 0, "Maya", 1);
        }
        Check(draft.Others() == DynastyCatalogue.OthersMost && draft.Bequests.Count == 1, "dynasty will: a share stops at the most the heir's part leaves (" + draft.Others() + ")");
        Check(DynastyWill.Move(draft, KinKind.Charity, 2, "Fund", 1) == 0 && draft.Bequests.Count == 1, "dynasty will: with the part full nobody else is added");
        DynastyWill.Move(draft, KinKind.Partner, 0, "Maya", -4);
        DynastyWill.Move(draft, KinKind.Charity, 2, "Fund", 3);
        Check(draft.Others() == 450 && draft.Find(KinKind.Charity, 2, null).Share == 150, "dynasty will: shares move a step at a time");
        Check(DynastyWill.Move(draft, KinKind.Child, 0, "Tom", 2) == 0 && draft.Find(KinKind.Child, 0, "Tom") == null, "dynasty will: the heir cannot be left a share");
        DynastyWill.SetHeir(draft, KinKind.Partner, 0, "Maya");
        Check(draft.Find(KinKind.Partner, 0, "Maya") == null && draft.Others() == 150, "dynasty will: naming somebody heir takes their bequest out");
        DynastyWill.Move(draft, KinKind.Charity, 2, "Fund", -3);
        Check(draft.Bequests.Count == 0, "dynasty will: a share brought to nothing is taken out");
        DynastyWill.RetireAge(draft, -40);
        DynastyWill.Keep(draft, 40);
        Check(draft.RetireAge == DynastyCatalogue.RetireLeast && draft.Keep == DynastyCatalogue.KeepSteps.Length - 1, "dynasty will: the age and the keep stop at their bounds");

        var st = new DynastyState();
        long paid = 0;
        Func<long, bool> pay = f => { paid += f; return true; };
        Will first = DynastyWillSample();
        first.Written = false;
        WillWhy why = DynastyWill.Sign(st, first, new Notary { Name = "Firm" }, 40, 1000, pay, out long fee);
        Check(why == WillWhy.Ok && fee == DynastyCatalogue.WillFee && paid == fee && st.Will.Written && st.Will.Day == 40 && st.Will.Firm == "Firm" && st.Will.Fees == fee,
            "dynasty will: the first will costs its fee and is signed (" + fee + ")");
        Check(DynastyWill.Sign(st, first, new Notary { Name = "Firm" }, 41, 1000, pay, out fee) == WillWhy.Same && fee == 0 && paid == DynastyCatalogue.WillFee,
            "dynasty will: signing the same terms again costs nothing and changes nothing");
        first.Bequests[0].Share = 150;
        Check(DynastyWill.Sign(st, first, new Notary { Name = "Firm" }, 42, 2000, pay, out fee) == WillWhy.Ok && fee == DynastyCatalogue.ChangeFee * 2 && st.Will.Bequests[0].Share == 150,
            "dynasty will: a change costs the smaller fee, at the city's prices (" + fee + ")");
        first.Bequests[0].Share = 100;
        long had = paid;
        Check(DynastyWill.Sign(st, first, new Notary(), 43, 1000, f => false, out fee) == WillWhy.Broke && st.Will.Bequests[0].Share == 150 && paid == had,
            "dynasty will: without the fee nothing is signed");
        Check(DynastyWill.Sign(st, first, new Notary { Own = true }, 43, 1000, null, out fee) == WillWhy.Ok && fee == 0, "dynasty will: the founder's own law firm charges nothing");
        first.Bequests[0].Share = 600;
        Check(DynastyWill.Sign(st, first, new Notary { Own = true }, 44, 1000, pay, out fee) == WillWhy.Over && st.Will.Bequests[0].Share == 100, "dynasty will: a draft that does not stand is not signed");
        first.Bequests[0].Share = 50;
        Check(st.Will.Bequests[0].Share == 100, "dynasty will: the signed will is a copy, so a draft changed later changes nothing signed");
    }

    // ---- who carries on

    private static List<KinPerson> DynastyFamily()
    {
        return new List<KinPerson>
        {
            new KinPerson { Kind = KinKind.Partner, Name = "Maya", Age = 58, Married = true, Closeness = 80 },
            new KinPerson { Kind = KinKind.Child, Name = "Tom", Age = 24, School = 2, FundMille = 1000, Closeness = 100 },
            new KinPerson { Kind = KinKind.Child, Name = "Ann", Age = 31, School = 1, FundMille = 0, Closeness = 20 },
            new KinPerson { Kind = KinKind.Child, Name = "Hana", Age = 15, School = 1, Closeness = 60 },
            new KinPerson { Kind = KinKind.Person, Id = 4, Name = "Nadia", Age = 44, Family = true, Closeness = 50, Household = true, Room = 900000000 },
            new KinPerson { Kind = KinKind.Person, Id = 7, Name = "Becker", Age = 40, Closeness = 90, Household = true, Room = 900000000 }
        };
    }

    private static void DynastyHeirChecks()
    {
        List<KinPerson> p = DynastyFamily();
        Will w = DynastyWillSample();
        Check(DynastyRules.Choose(w, p, out bool byLaw).Name == "Tom" && !byLaw, "dynasty heir: the will's heir carries on");
        Check(DynastyRules.Choose(new Will(), p, out byLaw).Name == "Maya" && byLaw, "dynasty heir: with no will the partner carries on by the law's order");
        var noPartner = p.Where(c => c.Kind != KinKind.Partner).ToList();
        Check(DynastyRules.Choose(new Will(), noPartner, out byLaw).Name == "Ann" && byLaw, "dynasty heir: then the eldest grown child");
        var onlyYoung = p.Where(c => c.Name == "Hana" || c.Kind == KinKind.Person).ToList();
        Check(DynastyRules.Choose(new Will(), onlyYoung, out byLaw) == null && byLaw, "dynasty heir: then nobody: a child not grown and a relative the will does not name do not carry on");
        var named = new Will { Written = true, HeirKind = KinKind.Person, HeirId = 4, HeirName = "Nadia" };
        Check(DynastyRules.Choose(named, onlyYoung, out byLaw).Name == "Nadia" && !byLaw, "dynasty heir: a relative carries on when the will names them");
        var friend = new Will { Written = true, HeirKind = KinKind.Person, HeirId = 7, HeirName = "Becker" };
        Check(DynastyRules.Choose(friend, p, out byLaw).Name == "Maya" && byLaw, "dynasty heir: somebody who is not family cannot carry on, so the law decides");
        var minor = new Will { Written = true, HeirKind = KinKind.Child, HeirName = "Hana" };
        Check(DynastyRules.Choose(minor, p, out byLaw).Name == "Maya" && byLaw, "dynasty heir: a named heir who is not grown yet gives way to the law's order");
        var gone = new Will { Written = true, HeirKind = KinKind.Child, HeirName = "Nobody" };
        Check(DynastyRules.Choose(gone, p, out byLaw).Name == "Maya" && byLaw, "dynasty heir: a named heir who is no longer there gives way too");
        var unsigned = new Will { HeirKind = KinKind.Child, HeirName = "Tom" };
        Check(DynastyRules.Choose(unsigned, p, out byLaw).Name == "Maya" && byLaw, "dynasty heir: a will that was never signed names nobody");
        Check(!DynastyRules.CanCarryOn(new KinPerson { Kind = KinKind.Partner, Name = "x", Age = 86 }) && DynastyRules.CanCarryOn(new KinPerson { Kind = KinKind.Partner, Name = "x", Age = 85 })
              && !DynastyRules.CanCarryOn(new KinPerson { Kind = KinKind.Child, Name = "x", Age = 17 }) && !DynastyRules.CanCarryOn(null),
            "dynasty heir: an heir is between eighteen and eighty-five");
        Check(DynastyRules.PartnerAge(0, 90) == 85 && DynastyRules.PartnerAge(0, 40) == 38 && DynastyRules.PartnerAge(51, 90) == 51, "dynasty heir: a partner whose age nobody keeps is a little under the founder's");
        Check(DynastyRules.ChildAge(18f, true, 100, 100 + 60 * 7 + 3, 60) == 25 && DynastyRules.ChildAge(11.7f, false, 0, 500, 60) == 11, "dynasty heir: a grown child's years go on after eighteen in the game's years");
        int best = DynastyRules.Upbringing(p[1]), worst = DynastyRules.Upbringing(new KinPerson { Kind = KinKind.Child, Name = "x", Age = 20 });
        Check(best == 1000 && worst == DynastyCatalogue.ChildBase, "dynasty heir: a child raised as well as can be knows all the founder knew, one left alone under a third (" + worst + ")");
        Check(DynastyRules.Upbringing(new KinPerson { Kind = KinKind.Child, School = 2 }) > DynastyRules.Upbringing(new KinPerson { Kind = KinKind.Child, School = 1 })
              && DynastyRules.Upbringing(new KinPerson { Kind = KinKind.Child, FundMille = 500 }) == DynastyCatalogue.ChildBase + DynastyCatalogue.ChildFund / 2
              && DynastyRules.Upbringing(new KinPerson { Kind = KinKind.Child, Closeness = 50 }) == DynastyCatalogue.ChildBase + DynastyCatalogue.ChildClose / 2,
            "dynasty heir: a private school, the college fund and closeness each add their part");
        Check(DynastyRules.Upbringing(p[0]) == DynastyCatalogue.PartnerBase + DynastyCatalogue.PartnerClose * 80 / 100 && DynastyRules.Upbringing(p[4]) == DynastyCatalogue.RelativeBase + DynastyCatalogue.RelativeClose / 2
              && DynastyRules.Upbringing(null) == 0, "dynasty heir: a partner and a relative start with their own parts");
        Check(DynastyRules.Passed(2400, 500) == 1200 && DynastyRules.Passed(2400, 1000) == 2400 && DynastyRules.Passed(2400, 4000) == 2400 && DynastyRules.Passed(-5, 500) == 0
              && Math.Abs(DynastyRules.Passed(80f, 250) - 20f) < 0.001f, "dynasty heir: what passes of a skill or of hours studied is the share, never more than the whole");
        Check(DynastyRules.FameKept(80) == 40 && DynastyRules.OwnAfter(90, 50) == 70 && DynastyRules.OwnAfter(10, 50) == 30, "dynasty heir: half the recognition stays with the name, and integrity goes half the way back");
    }

    // ---- an estate settled

    private static EstateIn DynastyEstate(long cash, long reserved, long other, long gifts = 0, HandMode mode = HandMode.Death, int keep = 0)
    {
        return new EstateIn { Day = 400, Level = 1000, Mode = mode, From = "Sam", Cash = cash, Reserved = reserved, Other = other, Gifts = gifts, Keep = keep, Charities = true };
    }

    private static Statement DynastySettle(EstateIn e, Will w, List<KinPerson> p)
    {
        KinPerson heir = DynastyRules.Choose(w, p, out bool byLaw);
        return DynastyRules.Settle(e, w, heir, byLaw, p);
    }

    private static void DynastySettleChecks()
    {
        List<KinPerson> p = DynastyFamily();
        Will w = DynastyWillSample();
        Statement s = DynastySettle(DynastyEstate(8000000, 0, 2000000), w, p);
        Check(DynastyRules.Balanced(s) && s.Net == 10000000 && s.To == "Tom" && !s.ByLaw && s.Lines.Count == 3, "dynasty estate: ten millions pass by the will");
        Check(s.Lines[0].Amount == 2000000 && s.Lines[0].Landing == Landing.Family && s.Lines[1].Amount == 1000000 && s.Lines[1].Landing == Landing.Pot
              && s.Lines[2].Amount == 500000 && s.Lines[2].Landing == Landing.Household && s.Lines[2].Id == 4 && s.Scale == 1000, "dynasty estate: each bequest is its share and lands where it can");
        Check(s.Charity == 1000000 && s.Spouse == 2000000 && s.Taxable == 7000000, "dynasty estate: the charity's bequest and the married partner's are taken off before the tax (" + s.Taxable + ")");
        Check(s.Tax == DynastyRules.Tax(7000000, 1000) && s.Tax == 200000 + 1200000 + 400000 && s.TaxCash == s.Tax && s.Loan == 0, "dynasty estate: the tax is the bands' on what is counted (" + s.Tax + ")");
        Check(s.Residue == 10000000 - s.Tax - 3500000 && DynastyRules.CashOut(s) <= 8000000, "dynasty estate: the heir is left the rest (" + s.Residue + ")");

        // the investors' floor
        Statement floor = DynastySettle(DynastyEstate(4000000, 1500000, 6000000), w, p);
        Check(floor.Net == 8500000 && floor.Reserved == 1500000 && DynastyRules.CashOut(floor) <= 2500000 && DynastyRules.Balanced(floor),
            "dynasty estate: cash the firms' investors are owed is not the estate's and is never paid out of");
        // debts first
        Statement debt = DynastySettle(DynastyEstate(8000000, 0, 2000000 - 2500000), w, p);
        Check(debt.Net == s.Net - 2500000 && debt.Tax < s.Tax && DynastyRules.Balanced(debt), "dynasty estate: a debt comes off the estate before anything is taxed or left");
        // no will
        Statement law = DynastySettle(DynastyEstate(8000000, 0, 2000000), new Will(), p);
        Check(law.ByLaw && law.To == "Maya" && law.SpouseHeir && law.Lines.Count == 0 && law.Spouse == 5000000 && law.Taxable == 5000000 && DynastyRules.Balanced(law),
            "dynasty estate: with no will the partner takes it all, and their relief covers half at most");
        Statement unwed = DynastyRules.Settle(DynastyEstate(8000000, 0, 2000000), new Will(), new KinPerson { Kind = KinKind.Partner, Name = "Maya", Age = 50 }, true, p);
        Check(unwed.Spouse == 0 && unwed.Taxable == 10000000, "dynasty estate: a partner who is not married has no relief");
        // the spouse's relief when they are left a bequest larger than half
        var big = new Will { Written = true, HeirKind = KinKind.Child, HeirName = "Tom" };
        big.Bequests.Add(new Bequest { Kind = KinKind.Partner, Name = "Maya", Share = 500 });
        Statement half = DynastySettle(DynastyEstate(9000000, 0, 1000000), big, p);
        Check(half.Spouse == 5000000 && half.Lines[0].Amount == 5000000, "dynasty estate: a married partner's bequest is exempt up to half the estate");
        // cash short: bequests cut alike, the tax whole
        Statement shortCash = DynastySettle(DynastyEstate(4000000, 0, 6000000), w, p);
        Check(shortCash.Scale < 1000 && shortCash.Scale > 0 && shortCash.Paid <= 4000000 - shortCash.TaxCash && shortCash.Loan == 0 && DynastyRules.Balanced(shortCash),
            "dynasty estate: when the cash after the tax does not cover the bequests each is cut by the same part (" + shortCash.Scale / 10 + "%)");
        Check(shortCash.Lines.All(l => l.Amount == l.Asked * shortCash.Scale / 1000) && shortCash.Tax == DynastyRules.Tax(shortCash.Taxable, 1000), "dynasty estate: the cut is the same thousandths of every bequest, and the tax is worked on what is really left");
        Statement noCash = DynastySettle(DynastyEstate(300000, 0, 9700000), w, p);
        Check(noCash.Scale == 0 && noCash.Paid == 0 && noCash.TaxCash == 300000 && noCash.Loan == noCash.Tax - 300000 && noCash.Loan > 0 && noCash.Tax == DynastyRules.Tax(10000000, 1000)
              && DynastyRules.Balanced(noCash), "dynasty estate: the tax is never cut: what the cash does not cover is owed (" + noCash.Loan + ")");
        // under water
        Statement water = DynastySettle(DynastyEstate(500000, 0, -3000000), w, p);
        Check(water.UnderWater && water.Net == -2500000 && water.Tax == 0 && water.Paid == 0 && water.Residue == -2500000 && water.Lines.Count == 0 && DynastyRules.Balanced(water),
            "dynasty estate: debts over the estate are not written off: the heir takes it all, and no bequest is paid");
        // retiring
        Statement retire = DynastySettle(DynastyEstate(8000000, 0, 2000000, 0, HandMode.Retire, 2), w, p);
        Check(retire.Keep == 1000000 && retire.Lines[0].Amount == 1800000 && retire.Taxable == 9000000 - 900000 - 1800000 && DynastyRules.Balanced(retire),
            "dynasty estate: a retiring founder keeps their part, and what passes is taxed by the same bands (" + retire.Tax + ")");
        Statement keepShort = DynastySettle(DynastyEstate(300000, 0, 9700000, 0, HandMode.Retire, 4), w, p);
        Check(keepShort.Keep == 300000 && DynastyRules.Balanced(keepShort), "dynasty estate: what is kept is cash, so never more than the cash there is");
        Check(DynastySettle(DynastyEstate(8000000, 0, 2000000, 0, HandMode.Death, 4), w, p).Keep == 0, "dynasty estate: nothing is kept back at a death");
        // landing
        var e = DynastyEstate(8000000, 0, 2000000);
        e.Charities = false;
        Statement closed = DynastySettle(e, w, p);
        Check(closed.Lines[1].Landing == Landing.None && closed.Lines[1].Amount == 0 && closed.Charity == 0 && closed.Taxable == 8000000,
            "dynasty estate: a charity that cannot take a bequest gets none, and nothing is exempt for it");
        e = DynastyEstate(8000000, 0, 2000000);
        e.Session = true;
        Statement session = DynastySettle(e, w, p);
        Check(session.Lines[1].Landing == Landing.None && session.Lines[2].Landing == Landing.Family && session.Lines[2].Amount == 500000,
            "dynasty estate: in a session nothing goes to the host's people or pots; a relative's part is kept on the family's own ledger");
        var stranger = new Will { Written = true, HeirKind = KinKind.Child, HeirName = "Tom" };
        stranger.Bequests.Add(new Bequest { Kind = KinKind.Person, Id = 7, Name = "Becker", Share = 100 });
        stranger.Bequests.Add(new Bequest { Kind = KinKind.Person, Id = 99, Name = "Gone", Share = 100 });
        List<KinPerson> p2 = DynastyFamily();
        p2[5].Household = false;
        Statement none = DynastySettle(DynastyEstate(8000000, 0, 2000000), stranger, p2);
        Check(none.Lines[0].Landing == Landing.None && none.Lines[1].Landing == Landing.None && none.Paid == 0 && none.Residue == 10000000 - none.Tax,
            "dynasty estate: a bequest with nobody there to receive it stays in the estate and is taxed with it");
        p2 = DynastyFamily();
        p2[4].Room = 1200;
        Statement room = DynastySettle(DynastyEstate(8000000, 0, 2000000), w, p2);
        Check(room.Lines[2].Asked == 1200 && room.Lines[2].Amount == 1200, "dynasty estate: a household is left no more than it has room for");
    }

    // Thousands of seeded estates: every one adds up, pays out no more cash than there is over the floor, and bears the tax its own figures give.
    private static void DynastyExploitChecks()
    {
        List<KinPerson> people = DynastyFamily();
        bool balanced = true, cashOk = true, taxOk = true, exemptOk = true, giftsOk = true, debtOk = true, linesOk = true;
        int cases = 0;
        for (int i = 0; i < 6000; i++)
        {
            long cash = DynastyRules.Hash(31, 1, i, 0) % 9000000u;
            long reserved = DynastyRules.Hash(31, 2, i, 0) % 3 == 0 ? DynastyRules.Hash(31, 3, i, 0) % 3000000u : 0;
            long other = (long)(DynastyRules.Hash(31, 4, i, 0) % 30000000u) - 6000000;
            long gifts = DynastyRules.Hash(31, 5, i, 0) % 4 == 0 ? DynastyRules.Hash(31, 6, i, 0) % 5000000u : 0;
            var w = new Will { Written = i % 5 != 0, HeirKind = i % 3 == 0 ? KinKind.Partner : KinKind.Child, HeirName = i % 3 == 0 ? "Maya" : "Tom" };
            int left = 0;
            for (int b = 0; b < 4; b++)
            {
                int share = (int)(DynastyRules.Hash(31, 7 + b, i, 0) % 5) * 50;
                if (share == 0 || left + share > DynastyCatalogue.OthersMost)
                {
                    continue;
                }
                left += share;
                w.Bequests.Add(b == 0 ? new Bequest { Kind = KinKind.Charity, Id = i % 3, Name = "c", Share = share }
                    : b == 1 ? new Bequest { Kind = KinKind.Person, Id = 4, Name = "Nadia", Share = share }
                    : b == 2 ? new Bequest { Kind = KinKind.Child, Name = "Ann", Share = share } : new Bequest { Kind = KinKind.Person, Id = 7, Name = "Becker", Share = share });
            }
            int level = 1000 + (int)(DynastyRules.Hash(31, 12, i, 0) % 1500);
            HandMode mode = i % 4 == 0 ? HandMode.Retire : HandMode.Death;
            var e = new EstateIn { Day = 500, Level = level, Mode = mode, Cash = cash, Reserved = reserved, Other = other, Gifts = gifts, Keep = i % 5, Charities = i % 7 != 0, Session = i % 11 == 0 };
            KinPerson heir = DynastyRules.Choose(w, people, out bool byLaw);
            Statement s = DynastyRules.Settle(e, w, heir, byLaw, people);
            cases++;
            long spare = Math.Max(0, cash - Math.Min(cash, reserved));
            balanced &= DynastyRules.Balanced(s) && s.Net == spare + other;
            cashOk &= DynastyRules.CashOut(s) <= spare && s.TaxCash <= spare - s.Keep;
            taxOk &= s.Tax == DynastyRules.Tax(s.Taxable, level) && s.Taxable == Math.Max(0, (s.UnderWater ? 0 : s.Net - s.Keep) - s.Charity - s.Spouse) + gifts;
            exemptOk &= s.Charity == s.Lines.Where(l => l.Landing == Landing.Pot).Sum(l => l.Amount) && s.Spouse <= Math.Max(0, (s.Net - s.Keep) / 2) && s.Spouse >= 0
                        && (s.Spouse == 0 || people[0].Married);
            linesOk &= s.Lines.All(l => l.Amount >= 0 && l.Amount <= l.Asked && (l.Landing != Landing.None || l.Amount == 0)) && s.Paid <= Math.Max(0, (s.Net - s.Keep) / 2) + 4
                       && (!s.UnderWater || (s.Paid == 0 && s.Keep == 0 && s.Residue == s.Net - s.Tax));
            // the same estate with a further gift of 100,000 made out of its cash just before: never less tax
            if (cash - reserved >= 100000)
            {
                var g = new EstateIn { Day = 500, Level = level, Mode = mode, Cash = cash - 100000, Reserved = reserved, Other = other, Gifts = gifts + 100000, Keep = e.Keep, Charities = e.Charities, Session = e.Session };
                Statement after = DynastyRules.Settle(g, w, heir, byLaw, people);
                giftsOk &= after.Tax >= s.Tax;
            }
            // ... and with 100,000 more debt: exactly that much less estate
            var d = new EstateIn { Day = 500, Level = level, Mode = mode, Cash = cash, Reserved = reserved, Other = other - 100000, Gifts = gifts, Keep = e.Keep, Charities = e.Charities, Session = e.Session };
            debtOk &= DynastyRules.Settle(d, w, heir, byLaw, people).Net == s.Net - 100000;
        }
        Check(balanced, "dynasty exploits: " + cases + " seeded estates each come to what is kept, the tax, the bequests paid and what the heir is left, to the last unit");
        Check(cashOk, "dynasty exploits: none pays out more cash than there is over the investors' floor");
        Check(taxOk, "dynasty exploits: each bears the bands' tax on its own figures: the estate less the exempt parts, the gifts counted in");
        Check(exemptOk, "dynasty exploits: only what really goes to a charity is exempt, and a married partner's relief is never over half");
        Check(linesOk, "dynasty exploits: no bequest is over its share, all together never over the heir's reserved half, and an estate under water pays none");
        Check(giftsOk, "dynasty exploits: giving money away just before never lowers the tax (the gift is counted back in)");
        Check(debtOk, "dynasty exploits: a debt lowers the estate by exactly itself: none is wiped");
        // splitting: ten gifts of a tenth against one gift, a gift on the last day of the window against the first
        var one = new List<LifeGift>();
        var ten = new List<LifeGift>();
        DynastyRules.Gift(one, 300, 2000000);
        for (int i = 0; i < 10; i++)
        {
            DynastyRules.Gift(ten, 291 + i, 200000);
        }
        Will none = new Will();
        long tOne = DynastySettle(DynastyEstate(3000000, 0, 0, DynastyRules.GiftsInWindow(one, 300, 60)), none, DynastyFamily()).Tax;
        long tTen = DynastySettle(DynastyEstate(3000000, 0, 0, DynastyRules.GiftsInWindow(ten, 300, 60)), none, DynastyFamily()).Tax;
        long tKept = DynastySettle(DynastyEstate(5000000, 0, 0), none, DynastyFamily()).Tax;
        Check(tOne == tTen && tOne >= tKept && tKept > 0, "dynasty exploits: an estate given away in one gift or in ten is taxed as if it had been kept (" + tOne + " against " + tKept + ")");
        Statement emptied = DynastySettle(DynastyEstate(0, 0, -50000, 8000000), none, DynastyFamily());
        Check(emptied.UnderWater && emptied.Tax == DynastyRules.Tax(8000000, 1000) && emptied.Loan == emptied.Tax && emptied.Residue == -50000 - emptied.Tax && DynastyRules.Balanced(emptied),
            "dynasty exploits: an estate emptied by gifts still bears their tax, owed by whoever carries on");
        // a gift to the heir does not come back: the heir is left the estate less the tax, with no term for what they were given
        Statement toHeir = DynastyRules.Settle(DynastyEstate(3000000, 0, 0, 2000000), none, DynastyFamily()[1], true, DynastyFamily());
        Check(toHeir.Residue == 3000000 - toHeir.Tax && toHeir.Tax == DynastyRules.Tax(5000000, 1000), "dynasty exploits: money given to the heir beforehand is taxed with the estate and is not handed to them again");
    }

    // ---- what holds a handover back and what refuses retiring

    private static void DynastyGateChecks()
    {
        Will w = DynastyWillSample();
        Check(DynastyRules.MayRetire(true, false, w, 60, true, false, false, false) == RetireWhy.Ok, "dynasty retiring: at the will's age with an heir and nothing in the way");
        Check(DynastyRules.MayRetire(false, false, w, 60, true, false, false, false) == RetireWhy.Off && DynastyRules.MayRetire(true, true, w, 60, true, false, false, false) == RetireWhy.Running,
            "dynasty retiring: not while the feature is off or an estate is passing");
        Check(DynastyRules.MayRetire(true, false, w, 59, true, false, false, false) == RetireWhy.Young && DynastyRules.MayRetire(true, false, new Will(), 80, true, false, false, false) == RetireWhy.NoWill,
            "dynasty retiring: not under the age the will sets, and not without a will");
        Check(DynastyRules.MayRetire(true, false, w, 60, false, false, false, false) == RetireWhy.NoHeir, "dynasty retiring: not with nobody to carry on");
        Check(DynastyRules.MayRetire(true, false, w, 60, true, true, false, false) == RetireWhy.Session, "dynasty retiring: not in a Going Public session");
        Check(DynastyRules.MayRetire(true, false, w, 60, true, false, true, false) == RetireWhy.Law, "dynasty retiring: not while the law has anything open: it is no way out of a sentence, a case or a ban");
        Check(DynastyRules.MayRetire(true, false, w, 60, true, false, false, true) == RetireWhy.Elder, "dynasty retiring: one retired founder at a time");
        Check(DynastyRules.Hold(true, false, false, false, true) == DeathHold.None, "dynasty last days: with an heir and nothing in the way the estate passes");
        Check(DynastyRules.Hold(true, true, true, true, true) == DeathHold.Final && DynastyRules.Hold(true, false, true, true, false) == DeathHold.NoHeir,
            "dynasty last days: Permadeath's death is its own, and with no heir the game's own end stands");
        Check(DynastyRules.Hold(true, false, true, true, true) == DeathHold.Session && DynastyRules.Hold(true, false, false, true, true) == DeathHold.Custody && DynastyRules.Hold(false, false, false, false, true) == DeathHold.Off,
            "dynasty last days: a session and a cell hold the estate back");
        Check(DynastyRules.Holds(DeathHold.Session) && DynastyRules.Holds(DeathHold.Custody) && !DynastyRules.Holds(DeathHold.Final) && !DynastyRules.Holds(DeathHold.NoHeir) && !DynastyRules.Holds(DeathHold.None)
              && !DynastyRules.Holds(DeathHold.Off), "dynasty last days: the age is held only for a session or a cell, never to cheat Permadeath or the game's own end");
        foreach (int year in new[] { 8, 60, 120, 365 })
        {
            int end = DynastyCatalogue.LastAge * year;
            Check(DynastyRules.HeldAge(year) + 2 < DynastyRules.LastDays(year) + 1 && DynastyRules.LastDays(year) < end && DynastyRules.HeldAge(year) > end - year,
                "dynasty last days: at " + year + " days a year the estate passes before the game's own end, and a held age stays short of it");
        }
        Check(DynastyCatalogue.LastAge == 90, "dynasty last days: the last age is the game's own (FuneralHelper.DeathAge)");
    }

    // ---- a handover in its steps

    private sealed class DynastyWorld : IHandWorld
    {
        public long Tax, Keep, Landed;
        public int TaxCalls, KeepCalls, LandCalls, Succeeded;
        public int Age = 3600;
        public long Refuse = -1;   // a line of this amount lands only half

        public int FounderAgeDays => Age;
        public void PayTax(long amount) { Tax += amount; TaxCalls++; }
        public void PayKeep(long amount) { Keep += amount; KeepCalls++; }
        public long Land(BequestLine line)
        {
            LandCalls++;
            long landed = line.Amount == Refuse ? line.Amount / 2 : line.Amount;
            Landed += landed;
            return landed;
        }
        public void Succeed(Statement s) { Succeeded++; }
    }

    private static DynastyState DynastyRun(Statement s, DynastyWorld world, bool saveBetween, out int steps)
    {
        var st = new DynastyState { Generation = 2, Will = DynastyWillSample() };
        st.Gifts.Add(new LifeGift { Day = 390, Amount = 5000 });
        s.Generation = 2;
        DynastyHand.Begin(st, s);
        steps = 0;
        for (int guard = 0; guard < 40 && st.Hand != null; guard++)
        {
            if (saveBetween)
            {
                st = DynastyStore.Read(DynastyStore.Write(st), out int _);   // a save and a load between every two steps
            }
            DynastyHand.Step(st, world, 400, 60, 5);
            steps++;
        }
        return st;
    }

    private static void DynastyHandChecks()
    {
        List<KinPerson> p = DynastyFamily();
        Will w = DynastyWillSample();
        var world = new DynastyWorld();
        Statement s = DynastySettle(DynastyEstate(8000000, 0, 2000000), w, p);
        DynastyState st = DynastyRun(s, world, false, out int steps);
        Check(st.Hand == null && steps == 6 && world.TaxCalls == 1 && world.KeepCalls == 0 && world.LandCalls == 3 && world.Succeeded == 1, "dynasty handover: a death passes in its steps: the tax, each bequest, the heir (" + steps + ")");
        Check(world.Tax == s.TaxCash && world.Landed == s.Paid && world.Tax + world.Landed == DynastyRules.CashOut(s), "dynasty handover: what leaves the wallet is the statement's cash, once");
        Check(st.Generation == 3 && !st.Will.Written && st.Gifts.Count == 0 && st.Line.Count == 1 && st.Line[0].Name == "Sam" && st.Line[0].Generation == 2 && st.Statements.Count == 1
              && st.Notes.Count == 1 && st.Notes[0].Who == -1 && st.Notes[0].To == "Tom", "dynasty handover: the heir is the head, the will and the gifts were the founder's, the statement and the lineage are kept");
        Check(st.KinOf(KinKind.Partner, "Maya", false) != null && st.KinOf(KinKind.Partner, "Maya", false).Held == 2000000 && st.Elder == null && st.Loan.Owed == 0,
            "dynasty handover: what a relative is left is on the family's ledger");
        var again = new DynastyWorld();
        Check(DynastyHand.Step(st, again, 401, 60, 5) && again.TaxCalls + again.LandCalls + again.Succeeded == 0, "dynasty handover: once over, a further step does nothing");
        Check(!DynastyHand.Begin(new DynastyState { Hand = new Handover() }, s), "dynasty handover: a second handover cannot begin while one is under way");

        var world2 = new DynastyWorld();
        DynastyState st2 = DynastyRun(DynastySettle(DynastyEstate(8000000, 0, 2000000), w, p), world2, true, out int steps2);
        Check(steps2 == steps && world2.Tax == world.Tax && world2.Landed == world.Landed && world2.TaxCalls == 1 && world2.LandCalls == 3 && world2.Succeeded == 1
              && DynastyStore.Write(st2) == DynastyStore.Write(st), "dynasty handover: saved and loaded between every two steps it ends exactly the same, nothing paid twice");

        // retiring: the fund, the elder
        var world3 = new DynastyWorld();
        Statement r = DynastySettle(DynastyEstate(8000000, 0, 2000000, 0, HandMode.Retire, 2), w, p);
        DynastyState st3 = DynastyRun(r, world3, true, out int _);
        Check(world3.KeepCalls == 1 && world3.Keep == 1000000 && st3.Elder != null && st3.Elder.Fund == 1000000 && st3.Elder.Name == "Sam" && st3.Elder.Born == 400 - 3600
              && st3.Elder.Taxable == r.Taxable && st3.Elder.Draw > 0, "dynasty handover: a retiring founder's part leaves the wallet for their own fund, and they stay as the family's elder");
        Check(world3.Tax + world3.Keep + world3.Landed == DynastyRules.CashOut(r), "dynasty handover: retiring takes the statement's cash, once");
        // the tax the cash does not cover goes onto the debt
        var world4 = new DynastyWorld();
        Statement owed = DynastySettle(DynastyEstate(300000, 0, 9700000), w, p);
        DynastyState st4 = DynastyRun(owed, world4, true, out int _);
        Check(world4.Tax == 300000 && st4.Loan.Principal == owed.Loan && st4.Loan.Owed == owed.Loan * 100 && st4.Loan.PerDay > 0, "dynasty handover: the tax the cash does not cover is owed, to the unit (" + owed.Loan + ")");
        // a bequest that lands short stays with the heir
        var world5 = new DynastyWorld { Refuse = 500000 };
        Statement part = DynastySettle(DynastyEstate(8000000, 0, 2000000), w, p);
        long residue = part.Residue;
        DynastyState st5 = DynastyRun(part, world5, false, out int _);
        Statement kept = st5.Statements[0];
        Check(kept.Lines[2].Amount == 250000 && kept.Residue == residue + 250000 && kept.Paid == 3250000 && DynastyRules.Balanced(kept) && world5.Landed == 3250000,
            "dynasty handover: a bequest that lands short costs the estate only what landed, and the rest stays with the heir");
        Check(DynastyHand.Steps(null) == 0 && DynastyHand.Done(null) == 0, "dynasty handover: no steps while none runs");
    }

    // ---- the debt for tax

    private static void DynastyLoanChecks()
    {
        const int year = 60;
        var l = new EstateLoan();
        DynastyRules.Borrow(l, 0, 10, year);
        Check(!l.Open, "dynasty debt: nothing owed, nothing borrowed");
        DynastyRules.Borrow(l, 900000, 10, year);
        Check(l.Open && l.Owed == 90000000 && l.Principal == 900000 && l.PerDay > 90000000 / (DynastyCatalogue.LoanYears * year), "dynasty debt: a part a day, sized for the interest (" + l.PerDay + " cents)");
        long paid = 0;
        int days = 0, most = DynastyCatalogue.LoanYears * year + 30;
        while (l.Open && days < most * 2)
        {
            paid += DynastyRules.LoanDay(l, long.MaxValue / 4, year, out bool forced);
            days++;
            if (forced)
            {
                break;
            }
        }
        Check(!l.Open && days <= most && days > DynastyCatalogue.LoanYears * year / 2, "dynasty debt: paid off in about its years (" + days + " days)");
        Check(paid > 90000000 && paid < 90000000L * (1000 + DynastyCatalogue.LoanRate * DynastyCatalogue.LoanYears) / 1000 && paid == l.PaidCents, "dynasty debt: with interest, and no more than the rate allows (" + paid / 100 + ")");
        var late = new EstateLoan();
        DynastyRules.Borrow(late, 100000, 10, year);
        long owedBefore = late.Owed;
        long none = 0;
        bool forcedEarly = false;
        for (int i = 0; i < DynastyCatalogue.LoanLateMost; i++)
        {
            none += DynastyRules.LoanDay(late, 0, year, out bool f);
            forcedEarly |= f;
        }
        Check(none == 0 && !forcedEarly && late.Late == DynastyCatalogue.LoanLateMost && late.Owed > owedBefore, "dynasty debt: a part the cash does not cover is a late day, and the debt goes on bearing interest");
        long taken = DynastyRules.LoanDay(late, 0, year, out bool forcedNow);
        Check(forcedNow && taken == late.PerDay, "dynasty debt: after too many late days in a row the part is taken whatever the cash");
        long ok = DynastyRules.LoanDay(late, long.MaxValue / 4, year, out bool forcedAfter);
        Check(ok > 0 && !forcedAfter && late.Late == 0, "dynasty debt: a part paid ends the run of late days");
        var two = new EstateLoan();
        DynastyRules.Borrow(two, 100000, 10, year);
        DynastyRules.Borrow(two, 50000, 20, year);
        Check(two.Principal == 150000 && two.Owed == 15000000 && two.Since == 10, "dynasty debt: a second estate's tax is added to the same debt");
    }

    // ---- the retired founder

    private static void DynastyElderChecks()
    {
        const int year = 60, month = 5;
        Elder e = DynastyRules.Retired("Alex", 62 * year, 1000, 1200000, 5000000, 1400000, year, month);
        int months = (DynastyCatalogue.LastAge - 62) * year / month;
        Check(e.Fund == 1200000 && e.Draw == 1200000 / months && e.Born == 1000 - 62 * year && DynastyRules.ElderAge(e, 1000, year) == 62, "dynasty elder: the fund is sized to last to the last age (" + e.Draw + " a month)");
        long d1 = DynastyRules.ElderMonth(e, 200), d2 = DynastyRules.ElderMonth(e, 200), d3 = DynastyRules.ElderMonth(e, 201);
        Check(d1 == e.Draw && d2 == 0 && d3 == e.Draw && e.Fund == 1200000 - 2 * e.Draw, "dynasty elder: they draw their living once a month");
        Check(!DynastyRules.ElderDies(e, e.Born + 90 * year - 1, year) && DynastyRules.ElderDies(e, e.Born + 90 * year, year) && !DynastyRules.ElderDies(null, 5000, year), "dynasty elder: they die at the game's own last age");
        var soon = DynastyRules.Retired("Alex", 88 * year, 1000, 2000000, 6000000, 1400000, year, month);
        Statement inside = DynastyRules.SettleElder(soon, "Sam", 1000 + 100, 1000, year, 1);
        Check(inside.Prior == 6000000 && inside.Tax == DynastyRules.Tax(8000000, 1000) - DynastyRules.Tax(6000000, 1000) && inside.Tax == 2000000L * 400 / 1000 && inside.Residue == 2000000 - inside.Tax
              && DynastyRules.Balanced(inside), "dynasty elder: a fund kept back is taxed with the estate handed over inside the window: no second threshold (" + inside.Tax + ")");
        Statement outside = DynastyRules.SettleElder(soon, "Sam", 1000 + DynastyRules.WindowDays(year), 1000, year, 1);
        Check(outside.Prior == 0 && outside.Tax == DynastyRules.Tax(2000000, 1000) && DynastyRules.Balanced(outside), "dynasty elder: past the window it is an estate of its own");
        long handed = DynastyRules.Tax(6000000, 1000);
        Check(handed + inside.Tax == DynastyRules.Tax(8000000, 1000), "dynasty elder: keeping a fund and dying soon after costs what passing it all at once would");
        var empty = DynastyRules.Retired("Alex", 70 * year, 1000, 0, 100, 0, year, month);
        Check(empty.Draw == 0 && DynastyRules.ElderMonth(empty, 1) == 0 && DynastyRules.SettleElder(empty, "Sam", 5000, 1000, year, 1).Residue == 0, "dynasty elder: a founder who kept nothing draws nothing and leaves nothing");
    }

    // ---- the rivals

    private static void DynastyRivalChecks()
    {
        const int year = 60;
        bool ages = true, same = true, later = true;
        int office = 0;
        for (int seed = 1; seed <= 300; seed++)
        {
            for (int t = 0; t < 3; t++)
            {
                var a = new RivalLine();
                var b = new RivalLine();
                DynastyRivals.Begin(a, seed, t, 500, year);
                DynastyRivals.Begin(b, seed, t, 500, year);
                int age = DynastyRivals.Age(a, 500, year);
                ages &= a.Ready && age >= DynastyCatalogue.RivalAgeFrom && age < DynastyCatalogue.RivalAgeFrom + DynastyCatalogue.RivalAgeSpan;
                same &= a.Born == b.Born && a.RetireAge == b.RetireAge && a.DeathAge == b.DeathAge;
                later &= a.DeathAge > age && (a.RetireAge == int.MaxValue || (a.RetireAge > age && a.DeathAge >= a.RetireAge + 2 && a.DeathAge <= 89));
                office += a.RetireAge == int.MaxValue ? 1 : 0;
                int born = a.Born;
                DynastyRivals.Begin(a, seed + 1, t, 900, year);
                same &= a.Born == born;
            }
        }
        Check(ages, "dynasty rivals: every tycoon begins between 45 and 64");
        Check(same, "dynasty rivals: the same seed gives the same ages on every machine, and a line once begun is not begun again");
        Check(later, "dynasty rivals: each retires and dies after the age they have, and dies after they retire");
        Check(office > 900 / DynastyCatalogue.RivalInOffice / 2 && office < 900 * 2 / DynastyCatalogue.RivalInOffice, "dynasty rivals: about one in " + DynastyCatalogue.RivalInOffice + " never retires and dies in office (" + office + " of 900)");
        var x = new RivalLine();
        var y = new RivalLine();
        DynastyRivals.Begin(x, 11, 0, 500, year);
        DynastyRivals.Begin(y, 12, 0, 500, year);
        Check(x.Born != y.Born || x.RetireAge != y.RetireAge || x.DeathAge != y.DeathAge, "dynasty rivals: another save has other ages");
        var began = new HashSet<int>();
        for (int seed = 1; seed <= 300; seed++)
        {
            var one = new RivalLine();
            DynastyRivals.Begin(one, seed, 0, 500, year);
            began.Add(DynastyRivals.Age(one, 500, year));
        }
        Check(began.Count >= DynastyCatalogue.RivalAgeSpan - 2, "dynasty rivals: the age a tycoon begins at comes from the save's seed (" + began.Count + " ages over 300 saves)");

        // two centuries of a line: every head hands over once, the generation counts on, the tax is the founder's own bands' and never takes a fortune under its floor
        int handovers = 0, retired = 0, died = 0, elders = 0;
        bool taxOk = true, order = true;
        for (int t = 0; t < 3; t++)
        {
            var r = new RivalLine();
            long worth = 80000000, floor = 50000000;
            DynastyRivals.Begin(r, 77, t, 100, year);
            int generation = 1;
            for (int day = 100; day < 100 + 200 * year; day++)
            {
                RivalEvent due = DynastyRivals.Due(r, day, year);
                if (due == RivalEvent.None)
                {
                    continue;
                }
                if (due == RivalEvent.ElderDied)
                {
                    DynastyRivals.ElderGone(r);
                    elders++;
                    continue;
                }
                long tax = DynastyRivals.TaxOf(worth, 1000), paid = DynastyRivals.Paid(tax, worth, floor);
                taxOk &= tax == DynastyRules.Tax(worth, 1000) && paid <= tax && worth - paid >= floor && paid == Math.Min(tax, worth - floor);
                worth -= paid;
                DynastyRivals.HandOver(r, due, 77, t, day, year, worth + paid, paid);
                generation++;
                handovers++;
                retired += due == RivalEvent.Retired ? 1 : 0;
                died += due == RivalEvent.Died ? 1 : 0;
                order &= r.Generation == generation && r.HandDay == day && DynastyRivals.Due(r, day, year) != due && DynastyRivals.Age(r, day, year) >= DynastyCatalogue.RivalHeirFrom
                         && DynastyRivals.Age(r, day, year) < DynastyCatalogue.RivalHeirFrom + DynastyCatalogue.RivalHeirSpan;
                worth += 60000000;   // the years between
            }
        }
        Check(handovers >= 12 && retired > 0 && retired + died == handovers, "dynasty rivals: over two centuries three lines hand over " + handovers + " times (" + retired + " retired, " + died + " died in office)");
        Check(elders > 0 && elders <= retired, "dynasty rivals: a retired head lives on and dies later (" + elders + ")");
        Check(taxOk, "dynasty rivals: their estate tax is the founder's own bands', and only the worth over their floor leaves them");
        Check(order, "dynasty rivals: an heir takes the holding with an age of their own, and the generation counts on");
        Check(DynastyRivals.Paid(500, 1000, 800) == 200 && DynastyRivals.Paid(500, 700, 800) == 0 && DynastyRivals.Paid(100, 1000, 800) == 100, "dynasty rivals: a tycoon at their floor pays nothing that would be made up the next day");
        var none = new RivalLine();
        Check(DynastyRivals.Due(none, 5000, year) == RivalEvent.None && DynastyRivals.Age(none, 5000, year) == 0, "dynasty rivals: a line not begun does nothing");
    }

    // ---- the city's estates

    private static void DynastyCityChecks()
    {
        var c = new CityEstates();
        DynastyCity.Turn(c, 4);
        long t1 = DynastyCity.Ended(c, 30000, 5000, 1000), t2 = DynastyCity.Ended(c, 2600000, 100000, 1000), t3 = DynastyCity.Ended(c, 4000, 9000, 1000);
        Check(t1 == 0 && t2 == DynastyRules.Tax(2500000, 1000) && t3 == 0, "dynasty city: a household's estate bears the founder's own bands (" + t2 + ")");
        Check(c.Count == 3 && c.Taxed == 1 && c.Value == 25000 + 2500000 && c.Tax == t2 && c.Unpaid == 5000, "dynasty city: the year's estates, the tax on them and the debts nothing was left to pay");
        DynastyCity.Turn(c, 4);
        Check(c.Count == 3, "dynasty city: the same year turns nothing");
        DynastyCity.Turn(c, 5);
        Check(c.Count == 0 && c.Value == 0 && c.LastCount == 3 && c.LastValue == 2525000 && c.LastTax == t2 && c.LastUnpaid == 5000 && c.Year == 5, "dynasty city: a new year starts the count again and keeps the last");
        DynastyCity.Turn(c, 9);
        Check(c.LastCount == 0 && c.LastValue == 0, "dynasty city: after years away the last year is not an old one");
    }

    // ---- the heir's learning in the mod's own education

    private static void DynastyStudyChecks()
    {
        var f = new StudyFile { LoanCents = 123456 };
        f.Skill[3] = 800;
        f.Best[3] = 900;
        f.Skill[7] = 1;
        f.Results.Add(new Enrolment { Course = 20, Stage = (int)EnrolStage.Passed, Grade = 2 });
        f.Results.Add(new Enrolment { Course = 21, Stage = (int)EnrolStage.Failed });
        f.Results.Add(new Enrolment { Course = 24, Stage = (int)EnrolStage.Passed, Grade = 1 });
        f.Courses.Add(new Enrolment { Course = 25, Stage = (int)EnrolStage.Studying });
        f.Children.Add(new ChildStudy { Name = "Tom", Course = 27, Stage = (int)ChildStep.Graduated, Start = 100, End = 220, Grade = 3 });
        f.Children.Add(new ChildStudy { Name = "Ann", Course = 22, Stage = (int)ChildStep.Studying });
        int gone = DynastyStudy.HandOver(f, "Tom", 500, 400);
        Check(gone == 2 && f.Skill[3] == 400 && f.Best[3] == 450 && f.Skill[7] == 0, "dynasty learning: the heir starts with their share of the founder's skills");
        Check(!f.Holds(20) && !f.Holds(24) && f.Holds(27) && f.GradeOf(27) == 3, "dynasty learning: the founder's qualifications do not pass on; the heir holds their own degree, with their own class");
        Check(f.Courses.Count == 0 && f.Results.Any(r => r.Course == 25 && r.Stage == (int)EnrolStage.Withdrawn && r.Ended == 400) && f.Results.Any(r => r.Course == 21 && r.Stage == (int)EnrolStage.Failed),
            "dynasty learning: the founder's course under way ends with them");
        Check(f.Children.Count == 0 && f.LoanCents == 123456, "dynasty learning: the children at university were the founder's, and the student loan is a debt that stays owed");
        var g = new StudyFile();
        g.Children.Add(new ChildStudy { Name = "Ann", Course = 22, Stage = (int)ChildStep.Studying });
        DynastyStudy.HandOver(g, "Ann", 1000, 10);
        Check(g.Results.Count == 0, "dynasty learning: a degree not finished is no degree");
        Check(DynastyStudy.HandOver(null, "x", 500, 1) == 0, "dynasty learning: with no study file there is nothing to hand over");
    }

    // ---- the save

    private static DynastyState DynastyFullState()
    {
        var s = new DynastyState { Seed = 4242, LastDay = 400, Generation = 3, RetireTold = 60, HeldTold = 399, PaperDay = 398, Will = DynastyWillSample() };
        s.Will.Day = 310;
        s.Will.Fees = 1500;
        s.Will.Firm = "Hud|son; Le^gal, & Co: v=1";
        s.Will.Bequests[2].Name = "Na|dia; Ha^dd,ad:x";
        s.Gifts.Add(new LifeGift { Day = 300, Amount = 5000 });
        s.Gifts.Add(new LifeGift { Day = 380, Amount = 250 });
        Statement st = DynastySettle(DynastyEstate(4000000, 500000, 6000000, 5250), s.Will, DynastyFamily());
        st.From = "Sa|m; R^i,v:era";
        s.Statements.Add(st);
        s.Statements.Add(DynastyRules.SettleElder(new Elder { Name = "Alex", Fund = 300000, Since = 100, Taxable = 4000000 }, "Sam", 150, 1200, 60, 2));
        DynastyHand.Begin(s, DynastySettle(DynastyEstate(2000000, 0, 8000000), s.Will, DynastyFamily()));
        s.Hand.TaxOut = true;
        s.Hand.Step = DynastyHand.StepKeep;
        s.Kin.Add(new Kin { Kind = KinKind.Child, Name = "Jo; x|y", Held = 320000, Day = 120 });
        s.Elder = new Elder { Name = "Al^ex", Born = -4800, Since = 120, Fund = 386000, Draw = 3900, Taxable = 5400000, Tax = 1470000, Month = 79 };
        DynastyRules.Borrow(s.Loan, 900000, 120, 60);
        s.Loan.Late = 2;
        s.Loan.PaidCents = 12345;
        DynastyRivals.Begin(s.RivalLines[0], 4242, 0, 400, 60);
        DynastyRivals.HandOver(s.RivalLines[0], RivalEvent.Retired, 4242, 0, 390, 60, 90000000, 31000000);
        DynastyRivals.Begin(s.RivalLines[2], 4242, 2, 400, 60);
        DynastyCity.Turn(s.City, 6);
        DynastyCity.Ended(s.City, 2600000, 100000, 1000);
        s.Line.Add(new LineEntry { Name = "Al|ex", Generation = 1, Day = 120, Mode = HandMode.Retire, Net = 6400000, Tax = 1470000 });
        s.Notes.Add(new DynastyNote { Day = 399, Who = 0, Mode = HandMode.Retire, Generation = 2, Tax = 31000000, From = "A; B", To = "C|D" });
        s.Grown.Add(new GrownChild { Name = "To;m", Day = 250 });
        return s;
    }

    private static void DynastyStoreChecks()
    {
        Check(DynastyStore.Key == "reallife:dynasty" && DynastyStore.Version == 1, "dynasty save: its own line, reallife:dynasty");
        Check(DynastyStore.Load(null, out int v0) == null && v0 == 0 && DynastyStore.Load(new Dictionary<string, string>(), out v0) == null
              && DynastyStore.Load(new Dictionary<string, string> { { DynastyStore.Key, "" } }, out v0) == null, "dynasty save: an old save has no line and loads with no will");
        Check(DynastyStore.Read("nonsense", out v0) == null && DynastyStore.Read("v=x^h:1", out v0) == null, "dynasty save: a line that is not the feature's is not read as one");
        DynastyState s = DynastyFullState();
        string line = DynastyStore.Write(s);
        DynastyState back = DynastyStore.Read(line, out int version);
        Check(version == 1 && back != null && DynastyStore.Write(back) == line, "dynasty save: everything written is read back the same (" + line.Length + " characters)");
        Check(back.Will.Firm == s.Will.Firm && back.Will.Bequests[2].Name == s.Will.Bequests[2].Name && back.Statements[0].From == s.Statements[0].From && back.Kin[0].Name == "Jo; x|y"
              && back.Elder.Name == "Al^ex" && back.Notes[0].To == "C|D" && back.Grown[0].Name == "To;m", "dynasty save: a name cannot break a record, whatever is in it");
        Check(back.Hand != null && back.Hand.TaxOut && !back.Hand.KeepOut && back.Hand.Step == DynastyHand.StepKeep && back.Hand.S.Lines.Count == 3 && DynastyRules.Balanced(back.Hand.S)
              && back.Hand.S.Tax == s.Hand.S.Tax, "dynasty save: a handover under way is kept with its step and its statement");
        Check(back.Will.Written && back.Will.HeirName == "Tom" && back.Will.Bequests.Count == 3 && back.Will.RetireAge == 60 && back.Will.Keep == 1 && back.Gifts.Count == 2 && back.Gifts[1].Amount == 250
              && back.Loan.Owed == s.Loan.Owed && back.Loan.Late == 2 && back.Elder.Fund == 386000 && back.Elder.Month == 79 && back.RivalLines[0].Generation == 2 && back.RivalLines[0].HandTax == 31000000
              && !back.RivalLines[1].Ready && back.RivalLines[2].Ready && back.City.Count == 1 && back.Line[0].Tax == 1470000 && back.Generation == 3 && back.Seed == 4242,
            "dynasty save: the will, the gifts, the debt, the elder, the rivals, the city and the lineage are all there");
        // a newer version: fields added at the end of records and a part this version does not know
        string newer = line.Replace("v=1^", "v=7^").Replace("^c:", "^z:new|part^c:").Replace("^h:4242|", "^h:4242|") + "|99|future^q:1|2|3";
        string headed = Regex.Replace(newer, @"\^w:([^\^]*)", m => "^w:" + m.Groups[1].Value + "|77|more");
        DynastyState later = DynastyStore.Read(headed, out int v7);
        Check(v7 == 7 && later != null && later.Will.HeirName == "Tom" && later.Will.Firm == s.Will.Firm && later.Statements.Count == 2 && later.City.Count == 1 && later.Grown.Count == 1,
            "dynasty save: a line of a newer version is read as far as this one knows it");
        // damage: figures are put inside their bounds
        DynastyState bent = DynastyStore.Read("v=1^h:5|-3|0|-9^w:1|10|9|0|999|77|-5|Tom^b:1|0|75|Maya;4|1|900|Trust;9|0|50|x^l:-5|-9|0^g:5|-10;6|40^r:1|2;^c:-1|-4|-4|-9", out int _);
        Check(bent != null && bent.LastDay == 0 && bent.Generation == 1 && bent.RetireTold == 0 && bent.Will.RetireAge == DynastyCatalogue.RetireMost && bent.Will.Keep == DynastyCatalogue.KeepSteps.Length - 1
              && bent.Will.Fees == 0 && bent.Will.HeirKind == KinKind.Person && DynastyRules.Valid(bent.Will) == WillWhy.Ok && bent.Loan.Owed == 0 && bent.Gifts.Count == 1 && bent.City.Count == 0,
            "dynasty save: a damaged line is read with every figure inside its bounds, and a will that cannot stand loses the bequests that break it");
        // a culture that writes a decimal comma and other digits groups
        CultureInfo before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            string german = DynastyStore.Write(s);
            DynastyState read = DynastyStore.Read(german, out int _);
            Thread.CurrentThread.CurrentCulture = new CultureInfo("ar-SA");
            Check(german == line && DynastyStore.Write(read) == line && DynastyStore.Write(DynastyStore.Read(line, out int _)) == line, "dynasty save: the line is the same under every culture");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
        // the option off: the line is written as it is and read again later
        var data = new Dictionary<string, string> { { DynastyStore.Key, line } };
        DynastyState off = DynastyStore.Load(data, out int _);
        Check(off != null && off.Will.Written && off.Hand != null && data[DynastyStore.Key] == line, "dynasty save: a will stays written with the option off, and a handover under way waits at its step");
    }

    // ---- the pages

    private static void DynastyPageChecks()
    {
        Check(UiScreens.All.Any(sc => sc.Id == "dynasty" && sc.Section == UiScreenSwitches.Ventures), "dynasty pages: the app has its own switch in Mod settings");
        foreach (string state in UiSamples.DynastyStates)
        {
            var stub = new UiSamples.DynastyStub();
            VentModel m = UiSamples.Dynasty(ModEnglish(), state, null, stub);
            bool off = state == "off";
            Check(off ? m.Shape == VentShape.Off : m.Shape == VentShape.Page && m.Nav.Length == 3, "dynasty pages: " + state + " builds as " + (off ? "the off page" : "a page of three tabs"));
            if (off)
            {
                continue;
            }
            int blocks = m.Left.Length + m.Right.Length;
            Check(blocks >= 6 && m.HeroFigures.Length == 3, "dynasty pages: " + state + " has its figures and blocks (" + blocks + ")");
            int primary = 0;
            foreach (VentBlock b in m.Right.Concat(m.Left))
            {
                if (b.Kind == VentBlockKind.Stepper)
                {
                    foreach (int run in new[] { b.Run, b.Run2 })
                    {
                        int had = stub.Count;
                        Check(run >= 0 && run < m.Runs.Length && m.Runs[run]() && stub.Count == had + 1, "dynasty pages: " + state + ": a step of '" + b.Text + "' reaches its call (" + stub.Last + ")");
                    }
                }
                if (b.Kind == VentBlockKind.Choice && !b.Disabled)
                {
                    int had = stub.Count;
                    Check(m.Runs[b.Run]() && stub.Count == had + 1 && stub.Last == "heir", "dynasty pages: " + state + ": '" + b.Text + "' reaches its call");
                }
                if (b.Kind != VentBlockKind.Actions)
                {
                    continue;
                }
                foreach (UiActionModel a in b.Actions)
                {
                    primary += a.Kind == UiButtonKind.Primary ? 1 : 0;
                    if (a.Disabled)
                    {
                        continue;
                    }
                    int before = stub.Count;
                    bool ran = a.Arg >= 0 && a.Arg < m.Runs.Length && m.Runs[a.Arg] != null && m.Runs[a.Arg]();
                    Check(ran && stub.Count == before + 1, "dynasty pages: " + state + ": the button '" + a.Label + "' reaches its call (" + stub.Last + ")");
                }
            }
            Check(primary <= 1, "dynasty pages: " + state + " has one primary action at most (" + primary + ")");
        }
        var press = new UiSamples.DynastyStub();
        VentModel will = UiSamples.Dynasty(ModEnglish(), "will", new VentView(), press);
        UiActionModel sign = will.Right.First(b => b.Kind == VentBlockKind.Actions).Actions.Last();
        Check(sign.Kind == UiButtonKind.Primary && sign.Disabled, "dynasty pages: with nothing changed the will cannot be signed again");
        VentModel dirty = UiSamples.Dynasty(ModEnglish(), "will_dirty", new VentView(), press);
        UiActionModel[] acts = dirty.Right.First(b => b.Kind == VentBlockKind.Actions).Actions;
        dirty.Runs[acts.Last().Arg]();
        Check(acts.Length == 2 && !acts.Last().Disabled && press.Last == "sign", "dynasty pages: a changed draft can be signed");
        dirty.Runs[acts[0].Arg]();
        Check(press.Last == "discard", "dynasty pages: or let go");
        VentModel pick = UiSamples.Dynasty(ModEnglish(), "will_pick", new VentView(), press);
        VentBlock choice = pick.Right.First(b => b.Kind == VentBlockKind.Choice);
        pick.Runs[choice.Run]();
        Check(press.Last == "heir" && press.Kind == KinKind.Person && press.Id == 4 && press.Name == "Nadia Haddad", "dynasty pages: a relative who can carry on can be named heir");
        VentBlock share = pick.Right.First(b => b.Kind == VentBlockKind.Stepper);
        pick.Runs[share.Run2]();
        Check(press.Last == "share" && press.Steps == 1 && press.Id == 4 && share.CanLess && share.CanMore && share.Value == "5%", "dynasty pages: their share moves a step");
        VentModel charity = UiSamples.Dynasty(ModEnglish(), "will_charity", new VentView(), press);
        Check(charity.Right.All(b => b.Kind != VentBlockKind.Choice) && charity.Right.Count(b => b.Kind == VentBlockKind.Stepper) == 3, "dynasty pages: a charity can be left a share but cannot carry on");
        charity.Runs[charity.Right.First(b => b.Kind == VentBlockKind.Stepper).Run2]();
        Check(press.Last == "share" && press.Kind == KinKind.Charity && press.Id == 1, "dynasty pages: the charity's share reaches its call");
        VentModel heirRow = UiSamples.Dynasty(ModEnglish(), "will", new VentView { Pick = DynastyPages.PersonKey + 1 }, press);
        Check(heirRow.Right.Count(b => b.Kind == VentBlockKind.Stepper) == 2, "dynasty pages: the heir has no share of their own to set: they take the rest");
        VentModel young = UiSamples.Dynasty(ModEnglish(), "will", new VentView { Pick = DynastyPages.PersonKey + 2 }, press);
        Check(young.Right.All(b => b.Kind != VentBlockKind.Choice), "dynasty pages: a child who is not grown cannot be named heir");
        VentModel estate = UiSamples.Dynasty(ModEnglish(), "estate", new VentView(), press);
        Check(estate.Right.First(b => b.Kind == VentBlockKind.Actions).Actions[0].Disabled, "dynasty pages: retiring is off while the law has something open");
        VentModel retire = UiSamples.Dynasty(ModEnglish(), "estate_retire", new VentView(), press);
        UiActionModel go = retire.Right.First(b => b.Kind == VentBlockKind.Actions).Actions[0];
        retire.Runs[go.Arg]();
        Check(!go.Disabled && press.Last == "retire", "dynasty pages: Retire and hand over reaches its call when nothing is in the way");
        Statement today = UiSamples.DynastyFactsOf("estate").Today;
        Check(estate.Left.Any(b => b.Kind == VentBlockKind.Row && b.Value != null && b.Value == UiSamples.VentWordsOf(ModEnglish()).Money(today.Tax)) && today.Tax > 0,
            "dynasty pages: the estate tab states the tax the shipped rule works out (" + today.Tax + ")");
        Check(UiSamples.Dynasty(ModEnglish(), "estate_handing", null, press).Left.Any(b => b.Kind == VentBlockKind.Steps && b.Count == 6 && b.Reached == 2), "dynasty pages: a handover under way shows its steps");
        Check(UiSamples.Dynasty(ModEnglish(), "estate_handing", null, press).Right.First(b => b.Kind == VentBlockKind.Actions).Actions[0].Disabled, "dynasty pages: nobody retires while an estate is passing");
        Check(UiSamples.Dynasty(ModEnglish(), "estate_loan", null, press).Right.Count(b => b.Kind == VentBlockKind.Row && b.Tone == UiRole.Warn) >= 1, "dynasty pages: a debt for tax is shown with what is owed");
        Check(UiSamples.DynastyFactsOf("estate_under").Today.UnderWater && UiSamples.Dynasty(ModEnglish(), "estate_under", null, press).Left.Any(b => b.Kind == VentBlockKind.Note && b.Tone == UiRole.Warn),
            "dynasty pages: an estate under water says that nothing is written off");
        Check(UiSamples.Dynasty(ModEnglish(), "line_elder", null, press).Left.Count(b => b.Kind == VentBlockKind.Row) >= 4, "dynasty pages: the family line shows the retired founder and what the family was left");
        string app = ModSource(@"Dynasty\DynastyApp.cs"), live = ModSource(@"Ui\Ventures\DynastyLive.cs");
        Check(app.Contains("live.Heir(") && app.Contains("live.Share(") && app.Contains("live.RetireAge(") && app.Contains("live.Keep(") && app.Contains("live.Sign()") && app.Contains("live.Discard()")
              && app.Contains("DynastyLive.Instance.Retire()") && live.Contains("DraftHeir(") && live.Contains("DraftShare(") && live.Contains("d.Sign(out long fee)") && live.Contains("d.Retire()")
              && live.Contains("GameConfirm.Show(\"reallife_dynasty_retire_ask_title\""), "dynasty pages: the app in the game's frame and the kit's page press the same calls, and retiring is asked once more");
        Check(ModSource(@"Family\FamilyPage.cs").Contains("FamilyBridges.WillLine") && ModSource(@"Family\FamilyPhone.Choices.cs").Contains("reallife_family_do_will_yes")
              && ModSource(@"Economy\Company\CompanyApp.Tax.cs").Contains("EconomyBridges.EstateTax") && ModSource(@"Rivals\RivalPage.cs").Contains("RivalBridges.DynastyLine"),
            "dynasty pages: the family side has the will's line, the tax side the estate tax, and the rich list the rivals' lines");
    }

    // ---- the sources

    private static void DynastySourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Dynasty\DynastyDirector.cs"));
        string[] files = Directory.GetFiles(folder, "*.cs");
        Check(files.Length >= 20, "dynasty sources: the feature's files are found (" + files.Length + ")");
        string all = string.Empty;
        string[] looks = { "appearanceSetter", "AppearanceSetter", "UpdateVisualAge", "SetAgeInDays", "blendshapes", ".elements", ".gender", "eyesColor", "fatness", "RandomizeElements", "SkinnedMeshRenderer",
            "HairLook", "CharacterQuality", "FamilyCast", "PersonLook" };
        foreach (string file in files.Concat(new[] { ModPath(@"Wiring\Wiring.DynastyLinks.cs"), ModPath(@"Family\FamilyDirector.Succession.cs"), ModPath(@"Careers\CareersDirector.Dynasty.cs"),
                     ModPath(@"Ui\Ventures\DynastyPages.cs"), ModPath(@"Ui\Ventures\DynastyLive.cs") }))
        {
            string text = File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            all += text;
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("Update()") && !text.Contains("FindObjectsByType"), "dynasty sources: " + name + " books no money outside the one call and has no per-frame code or scene search");
            Check(!text.Contains("ProjectSettings") && !text.Contains("TODO") && !text.Contains("NotImplemented"), "dynasty sources: " + name + " has no stub and no note of work left");
            Check(!looks.Any(text.Contains), "dynasty sources: " + name + " reads and writes nothing of how a character looks");
            Check(!text.Contains("UnityEngine.Random") || name == "DynastyDirector.cs", "dynasty sources: " + name + " draws nothing at random (the one seed of a save apart)");
        }
        string game = ModSource(@"Dynasty\DynastyGame.cs"), hand = ModSource(@"Dynasty\DynastyDirector.Hand.cs"), dir = ModSource(@"Dynasty\DynastyDirector.cs");
        Check(CountOfText(all, "BizBooks.Book(") == 3 && CountOfText(game, "BizBooks.Book(") == 3, "dynasty sources: money moves only through the mod's one call, in the one file that touches the game");
        Check(CountOfText(all, "DynastyGame.Receive(") == 1 && hand.Contains("DynastyGame.Receive(s.Residue, DynastyGame.FundKey)"), "dynasty sources: the one thing that comes into the wallet is what a retired founder's own fund leaves");
        Check(CountOfText(game, "c.name = name") == 1 && CountOfText(game, "ageInDays = ") == 4 && game.Contains("d.minutesStudied = DynastyRules.Passed(had, share)") && game.Contains("skill.value = DynastyRules.Passed(skill.value, share)"),
            "dynasty sources: the heir takes the name, the age and their share of the game's skills and courses, and nothing else of the character");
        Check(CountOfText(dir, "UnityEngine.Random.Range(1, int.MaxValue)") == 1 && dir.Contains("if (_state.Seed == 0)"), "dynasty sources: one seed a save, drawn once and kept");
        Check(dir.Contains("StartCoroutine(Guard.Routine(Tick()))") && dir.Contains("catch (Exception e)") && dir.Contains("WaitForSecondsRealtime(1f)"), "dynasty sources: the timer is a guarded coroutine, once a second");
        int retire = hand.IndexOf("public RetireWhy Retire()", StringComparison.Ordinal);
        Check(retire > 0 && hand.IndexOf("RetireWhyNow(out", retire, StringComparison.Ordinal) < hand.IndexOf("Begin(HandMode.Retire", retire, StringComparison.Ordinal)
              && hand.Contains("GoingPublic.InSession, Ask(DynastyBridges.LawOpen)") && hand.Contains("if (why == RetireWhy.Ok)"), "dynasty sources: retiring is refused before the estate is frozen or any money booked");
        Check(hand.Contains("DynastyRules.Hold(Running, Ask(DynastyBridges.Final), GoingPublic.InSession, Ask(DynastyBridges.Custody), heir != null)") && hand.Contains("DynastyGame.SetAgeDays(DynastyRules.HeldAge(year))")
              && hand.Contains("if (DynastyRules.Holds(hold))"), "dynasty sources: the last day is held only by the rule, in a session or a cell; Permadeath's death and the game's own end are left alone");
        Check(dir.Contains("if (_state.Hand != null)") && dir.Contains("HandStep();") && hand.Contains("DynastyHand.Step(_state, this,"), "dynasty sources: a handover under way goes on at its step after a load");
        string putAway = dir.Substring(dir.IndexOf("private void PutAway()", StringComparison.Ordinal), 400);
        Check(putAway.Contains("Write();") && putAway.Contains("RemoveApp();") && !putAway.Contains("new DynastyState") && !putAway.Contains("Hand = null"), "dynasty sources: with the option off the line stays as it is");
        Check(dir.Contains("DynastyRules.Gift(kept.Gifts, DynastyGame.Day, amount);") && dir.Contains("save.modData[DynastyStore.Key] = DynastyStore.Write(kept);"), "dynasty sources: a gift is dated in the save's line with the option off too, so the option is no way round the window");
        string wiring = ModSource(@"Wiring\Wiring.DynastyLinks.cs"), gifts = ModSource(@"Giving\GivingDirector.Gifts.cs");
        Check(wiring.Contains("Settings.Permadeath && PermadeathState.Armed") && wiring.Contains("JusticeState.OnProbation(day)") && wiring.Contains("JusticeState.Suspended(day)") && wiring.Contains("JusticeState.OpenCount > 0")
              && wiring.Contains("Licences.Driving.Suspended") && wiring.Contains("LawState.WantedCharges != 0"), "dynasty sources: Permadeath's switch, and every open matter of the law, are asked where they are kept");
        Check(wiring.Contains("JusticeState.Seal();") && !wiring.Contains("JusticeState.Clear()") && !wiring.Contains("Debts.Clear") && !wiring.Contains("Cases.Clear") && !wiring.Contains("ProbationUntil =")
              && !wiring.Contains("SuspendedUntil =") && !wiring.Contains("CommunityHours ="), "dynasty sources: the closed record is sealed; no case, fine, sentence or ban is touched");
        Check(wiring.Contains("GoingPublic.InSession ? RivalDirector.Instance.File : null") && wiring.Contains("DynastyRivals.Paid(tax, t.Worth, RivalRules.At(t.Def.StartWorth, Prices.Level))") && wiring.Contains("t.Worth -= paid;"),
            "dynasty sources: the rivals rest in a session, and what leaves a fortune is exactly what the rule says was paid");
        Check(wiring.Contains("GivingGame.People()") && wiring.Contains("GivingPeople.Helpable(pop, h)") && wiring.Contains("GivingPeople.Help(pop, h, lands, out long _, out long _);"), "dynasty sources: a bequest reaches a household through the population's own fields, where the city's people are this player's");
        int bequest = gifts.IndexOf("public long Bequest(", StringComparison.Ordinal);
        string body = gifts.Substring(bequest, gifts.IndexOf("public void Merged(", StringComparison.Ordinal) - bequest);
        Check(bequest > 0 && body.Contains("_state.Credit(charity, amount, 0);") && !body.Contains("TaxGifts") && !body.Contains("Reward(") && !body.Contains("ClaimNow") && !body.Contains("GivingGame.Pay"),
            "dynasty sources: a charity's bequest goes in its pot and is no deduction from the income tax: it is exempt once, from the estate");
        Check(gifts.Contains("GivingBridges.GaveToPerson?.Invoke(amount);") && ModSource(@"Giving\GivingDirector.Loans.cs").Contains("GivingBridges.GaveToPerson?.Invoke(Math.Max(0, l.Remaining));")
              && wiring.Contains("GivingBridges.GaveToPerson = amount => DynastyDirector.Instance?.Gave(amount);"), "dynasty sources: every gift to a person, and a loan written off, is told for the window");
        Check(ModSource(@"Population\LifeRules.cs").Contains("Ended?.Invoke(hh.Savings[h], hh.Debt[h]);") && wiring.Contains("Population.LifeRules.Ended = "), "dynasty sources: the population tells a household that ended with a death");
        Check(CountOfText(ModSource(@"Wiring\Wiring.cs"), "DynastyLinks") == 2, "dynasty sources: the wiring connects and disconnects the dynasty");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("Dynasty.DynastyGuide.Register()") && ModSource(@"Core\FeatureOwners.cs").Contains("\"RealLifeOverhaul.Dynasty\", \"dynasty\"") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"dynasty\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"dynasty\"") && ModSource(@"Core\Settings.cs").Contains("public static bool Dynasty = true;"),
            "dynasty sources: the feature has its option (on unless switched off), its Guide page, its owner and its place in the cities list");
        string tests = ModSource(@"Dynasty\DynastyDirector.Tests.cs"), guide = ModSource(@"Dynasty\DynastyGuide.cs");
        Check(tests.Contains("public void TestStatement()") && tests.Contains("public void TestRetireAge()") && tests.Contains("public void TestLastDays()") && tests.Contains("public void TestRival()")
              && guide.Contains("TestStatement()") && guide.Contains("TestRetireAge()") && guide.Contains("TestLastDays()") && guide.Contains("TestRival()") && CountOfText(tests, "Dynasty check (test") == 5,
            "dynasty sources: Options, Tests has its four entries, each with its check line");
    }

    // ---- the texts

    private static void DynastyTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string[] sources = { @"Dynasty\DynastyDirector.cs", @"Dynasty\DynastyDirector.App.cs", @"Dynasty\DynastyDirector.Facts.cs", @"Dynasty\DynastyDirector.Hand.cs", @"Dynasty\DynastyDirector.Will.cs",
            @"Dynasty\DynastyDirector.Rivals.cs", @"Dynasty\DynastyDirector.Tests.cs", @"Dynasty\DynastyGuide.cs", @"Dynasty\DynastyText.cs", @"Dynasty\DynastyApp.cs", @"Dynasty\DynastyGame.cs",
            @"Ui\Ventures\DynastyPages.cs", @"Ui\Ventures\DynastyLive.cs", @"Wiring\Wiring.DynastyLinks.cs", @"Family\FamilyDirector.Succession.cs" };
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(ModSource(source), "\"(reallife_[a-z0-9_]+)\""))
            {
                if (!m.Groups[1].Value.StartsWith("reallife_giving_", StringComparison.Ordinal) && !m.Groups[1].Value.StartsWith("reallife_ui_", StringComparison.Ordinal))
                {
                    keys.Add(m.Groups[1].Value);
                }
            }
        }
        foreach (string k in new[] { "reallife_opt_sec_dynasty", "reallife_opt_tip_dynasty", "reallife_ui_screen_dynasty", "reallife_opt_tip_ui_screen_dynasty", "reallife_family_do_will_yes",
                     "reallife_family_do_will_where", "reallife_dynasty_tax_row", "reallife_transaction_dynasty_tax_label", "reallife_transaction_dynasty_tax_part_label",
                     "reallife_transaction_dynasty_bequest_label", "reallife_transaction_dynasty_charity_label", "reallife_transaction_dynasty_keep_label", "reallife_transaction_dynasty_fund_label",
                     "reallife_transaction_dynasty_will_label" })
        {
            keys.Add(k);
        }
        Check(keys.Count >= 190, "dynasty texts: its keys are found: " + keys.Count);
        string[] notices = { "reallife_dynasty_retire_ready", "reallife_dynasty_held_session", "reallife_dynasty_held_custody", "reallife_dynasty_end_final", "reallife_dynasty_end_no_heir",
            "reallife_dynasty_hand_death", "reallife_dynasty_hand_retire", "reallife_dynasty_hand_done", "reallife_dynasty_loan_forced", "reallife_dynasty_loan_late", "reallife_dynasty_loan_paid",
            "reallife_dynasty_elder_died", "reallife_dynasty_signed", "reallife_dynasty_will_too_many", "reallife_dynasty_will_share", "reallife_dynasty_will_over", "reallife_dynasty_will_heir",
            "reallife_dynasty_will_age", "reallife_dynasty_will_keep", "reallife_dynasty_will_same", "reallife_dynasty_will_broke", "reallife_dynasty_will_off", "reallife_dynasty_retire_off",
            "reallife_dynasty_retire_running", "reallife_dynasty_retire_young", "reallife_dynasty_retire_no_heir", "reallife_dynasty_retire_session", "reallife_dynasty_retire_law",
            "reallife_dynasty_retire_elder", "reallife_dynasty_retire_no_will", "reallife_dynasty_test_statement", "reallife_dynasty_test_age", "reallife_dynasty_test_last", "reallife_dynasty_test_rival",
            "reallife_dynasty_test_no_rival", "reallife_dynasty_test_off", "reallife_dynasty_retire_ask" };
        string[] facts = { "{dynasty_threshold}", "{dynasty_rate_low}", "{dynasty_rate_mid}", "{dynasty_rate_top}", "{dynasty_band_low}", "{dynasty_band_mid}", "{dynasty_window}", "{dynasty_spouse}",
            "{dynasty_others}", "{dynasty_fee}", "{dynasty_change_fee}", "{dynasty_retire_least}", "{dynasty_keep_most}", "{dynasty_last_age}", "{dynasty_loan_rate}", "{dynasty_loan_years}", "{dynasty_fame}" };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        foreach (string code in DynastyLanguages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "dynasty texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "dynasty texts: " + key + " is in " + code + ".json");
                if (!there)
                {
                    continue;
                }
                Check(!value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - "), "dynasty texts: " + key + " has no exclamation mark or dash in " + code);
                if (english != null && english.TryGetValue(key, out string en))
                {
                    var wanted = new SortedSet<string>(Regex.Matches(en, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    var got = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    Check(wanted.SetEquals(got), "dynasty texts: " + key + " keeps its {names} in " + code);
                    Check(Regex.Matches(en, "<b>").Count == Regex.Matches(value, "<b>").Count && Regex.Matches(en, @"\n").Count == Regex.Matches(value, @"\n").Count, "dynasty texts: " + key + " keeps its tags and lines in " + code);
                }
            }
            foreach (string key in notices)
            {
                Check(words.TryGetValue(key, out string value) && value.Length <= 240, "dynasty texts: the notice " + key + " is at most 240 characters in " + code);
            }
            if (words.TryGetValue("reallife_guide_dynasty", out string page))
            {
                foreach (string fact in facts)
                {
                    Check(page.Contains(fact), "dynasty texts: the Guide page has " + fact + " in " + code);
                }
            }
            Check(words.TryGetValue("reallife_dynasty_app", out string appName) && words.TryGetValue("reallife_ui_screen_dynasty", out string screenName) && appName == screenName
                  && page != null && page.Contains("<b>" + appName + "</b>"), "dynasty texts: the app has one name in " + code);
        }
        string guide = ModSource(@"Dynasty\DynastyGuide.cs");
        foreach (string fact in facts)
        {
            Check(guide.Contains(".Fact(\"" + fact.Trim('{', '}') + "\""), "dynasty texts: the Guide's " + fact + " is filled from the rules");
        }
        // The pages' own English is the locale's, so a preview and the game never differ.
        string pages = ModSource(@"Ui\Ventures\DynastyPages.cs");
        int same = 0, differ = 0;
        foreach (Match m in Regex.Matches(pages, "w\\.[TF]\\(\\s*\"(reallife_dynasty_[a-z0-9_]+)\",\\s*((?:\"(?:[^\"\\\\]|\\\\.)*\"\\s*(?:\\+\\s*)?)+)"))
        {
            string text = string.Concat(Regex.Matches(m.Groups[2].Value, "\"((?:[^\"\\\\]|\\\\.)*)\"").Cast<Match>().Select(x => Regex.Unescape(x.Groups[1].Value)));
            bool equal = english != null && english.TryGetValue(m.Groups[1].Value, out string en) && en == text;
            same += equal ? 1 : 0;
            differ += equal ? 0 : 1;
        }
        Check(same >= 95 && differ == 0, "dynasty texts: every English text the pages carry is the locale's own (" + same + " the same, " + differ + " not)");
        // What's new: the feature's line in the next row of every language, before the first fix mark.
        const string sentence = "Dynasty: write a will in the new Legacy app";
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        for (int i = 0; i < DynastyLanguages.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, DynastyLanguages[i] + ".json"));
            string next = words != null && words.TryGetValue("reallife_changes_next", out string value) ? value : string.Empty;
            int mark = next.IndexOf(marks[i], StringComparison.Ordinal);
            string before = mark >= 0 ? next.Substring(0, mark) : next;
            if (i == 0)
            {
                Check(next.Contains(sentence) && next.IndexOf(sentence, StringComparison.Ordinal) < (mark < 0 ? int.MaxValue : mark), "dynasty texts: What's new has the feature's line before the first fix mark in en");
            }
            else
            {
                string app = words != null && words.TryGetValue("reallife_dynasty_app", out string name) ? name : "(no name)";
                Check(!next.Contains(sentence) && before.Contains(app), "dynasty texts: What's new has the feature's line, translated and naming the app, before the first fix mark in " + DynastyLanguages[i]);
            }
        }
    }
}
